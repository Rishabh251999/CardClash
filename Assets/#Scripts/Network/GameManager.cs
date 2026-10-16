using Mirror;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace CardClash
{
    public class GameManager : MonoBehaviour
    {
        #region constants/readonly

        #endregion

        #region Script References

        [Header("Script References")]
        [SerializeField] private LobbyManager _lobbyManager;
        [SerializeField] private RoomManager _roomManager;
        [SerializeField] private UIManager _uiManager;

        #endregion

        #region GUI References

        [Space(5f)]

        [Header("GUI References")]
        [SerializeField] private CardGameController matchControllerPrefab;

        #endregion

        #region Runtime State

        private RoomService _roomService;
        private MatchService _matchService;
        private readonly ServerRoomRepository _serverRepo = new();
        private readonly ClientRoomRepository _clientRepo = new();
        internal ClientRoomRepository ClientRoomRepository => _clientRepo;

        private PlayerReconnectionService _reconnectService;

        private int playerIndex = 1;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _roomService = new(_serverRepo, () => SendRoomList());

            _matchService = new(_serverRepo, matchControllerPrefab, () => SendRoomList());

            _reconnectService = new(_serverRepo, this, () => SendRoomList());

            _lobbyManager.Initialize(_clientRepo);
        }

        internal void InitializeServerData()
        {
            _serverRepo.Clear();
            playerIndex = 1;
        }

        #endregion

        #region Server Callbacks

        [ServerCallback]
        internal void OnStartServer()
        {
            InitializeServerData();
            NetworkServer.RegisterHandler<ServerRoomMessage>(OnServerRoomMessage);
            NetworkServer.RegisterHandler<ServerDeckMessage>(OnServerDeckMessage);
            NetworkServer.RegisterHandler<SetPlayerInfoMessage>(OnSetPlayerInfo);
        }

        [ServerCallback]
        internal void OnServerReady(NetworkConnectionToClient conn)
        {
            _serverRepo.WaitingConnections.Add(conn);

            if (!_serverRepo.PlayerInfos.ContainsKey(conn))
            {
                _serverRepo.PlayerInfos.Add(conn, new()
                {
                    playerId = playerIndex,
                    isReady = false,
                });
            }

            SendRoomList();
        }

        [ServerCallback]
        internal IEnumerator OnServerDisconnect(NetworkConnectionToClient conn)
        {
            _serverRepo.PlayerInfos.TryGetValue(conn, out PlayerRoomInfo playerInfo);

            var roomCode = playerInfo.roomCode;

            // Recover the room code if player information is incomplete.
            if (roomCode == Guid.Empty && _serverRepo.PlayerRooms.TryGetValue(conn, out var mappedRoom))
            {
                roomCode = mappedRoom;
            }

            // CASE 1: The player belongs to an active match.
            if (roomCode != Guid.Empty && _serverRepo.MatchControllers.TryGetValue(roomCode, out var matchController))
            {
                if (playerInfo.isOwner)
                {
                    // Owner disconnecting ends the match.
                    _matchService.EndMatch(matchController);
                }
                else
                {
                    // Non-owner disconnecting leaves the active match.
                    matchController.HandlePlayerQuit(conn);

                    playerInfo.isReady = false;
                    playerInfo.roomCode = Guid.Empty;
                    _serverRepo.PlayerInfos[conn] = playerInfo;
                }
            }
            // CASE 2: The owner disconnects from a waiting room.
            else if (playerInfo.isOwner &&
                     _serverRepo.PlayerRooms.ContainsKey(conn))
            {
                _roomService.CancelRoom(conn);
            }
            // CASE 3: A non-owner disconnects from a waiting room.
            else if (roomCode != Guid.Empty && _serverRepo.OpenRooms.ContainsKey(roomCode))
            {
                _reconnectService.HandleDisconnect(conn, playerInfo, roomCode);
            }

            // Remove the disconnected connection's server records.
            _serverRepo.PlayerRooms.Remove(conn);
            _serverRepo.PlayerInfos.Remove(conn);
            _serverRepo.WaitingConnections.Remove(conn);

            SendRoomList();

            yield return null;
        }


        [ServerCallback]
        private void OnSetPlayerInfo(NetworkConnectionToClient conn, SetPlayerInfoMessage msg)
        {
            var username = msg.Username.Trim();

            if (_reconnectService.TryReconnect(conn, msg.SessionId, username))
                return;

            var info = _serverRepo.PlayerInfos.TryGetValue(conn, out var existing) ? 
                existing : new() { playerId = playerIndex += 1 };
            info.playerName = username;
            info.sessionId = msg.SessionId;
            _serverRepo.PlayerInfos[conn] = info;
        }

        [ServerCallback]
        internal void OnStopServer() => InitializeServerData();

        #endregion

        #region Client Callbacks

        [ClientCallback]
        internal void OnStartClient()
        {
            Card.PopulateCardSprites();

            _clientRepo.Clear();

            _lobbyManager.UpdateRoomList(_clientRepo.OpenRooms);

            NetworkClient.RegisterHandler<ClientRoomMessage>(OnClientRoomMessage);
            NetworkClient.RegisterHandler<ClientDeckMessage>(OnClientDeckMessage);
        }

        [ClientCallback]
        internal void OnClientConnect()
        {
            var username = PlayerPrefs.GetString("UserName", string.Empty);

            var sessionId = PlayerPrefs.GetString("SessionId", string.Empty);
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("SessionId", sessionId);
            }

            NetworkClient.Send(new SetPlayerInfoMessage { Username = username, SessionId = sessionId });
        }

        [ClientCallback]
        internal void OnClientDisconnect()
        {
            _clientRepo.Clear();
        }

        [ClientCallback]
        internal void OnStopClient()
        {
            _clientRepo.Clear();
        }

        #endregion

        #region Server Message Handler

        [ServerCallback]
        private void OnServerRoomMessage(NetworkConnectionToClient conn, ServerRoomMessage msg)
        {
            switch (msg.serverRoomOperation)
            {
                case ServerRoomOperation.Create:
                    _roomService.CreateRoom(
                        conn, msg.maxPlayers, msg.startingCards);
                    break;

                case ServerRoomOperation.Join:
                    _roomService.JoinRoom(conn, msg.roomCode);
                    break;

                case ServerRoomOperation.Leave:
                    _roomService.LeaveRoom(conn);
                    break;

                case ServerRoomOperation.Cancel:
                    _roomService.CancelRoom(conn);
                    break;

                case ServerRoomOperation.List:
                    SendRoomList(conn);
                    break;

                case ServerRoomOperation.Ready:
                    _roomService.ToggleReady(conn, msg.roomCode);
                    break;

                case ServerRoomOperation.Start:
                    _matchService.StartMatch(conn);
                    break;
            }
        }

        [ServerCallback]
        private bool TryGetMatchController(NetworkConnectionToClient conn, out CardGameController controller)
        {
            controller = null;

            // Find which room this connection's player is in via NetworkMatch
            if (conn.identity == null) return false;

            if (!conn.identity.TryGetComponent<NetworkMatch>(out var networkMatch))
                return false;

            Guid roomCode = networkMatch.matchId;
            return _serverRepo.MatchControllers.TryGetValue(roomCode, out controller);
        }

        [ServerCallback]
        void OnServerDeckMessage(NetworkConnectionToClient conn, ServerDeckMessage msg)
        {
            if (!TryGetMatchController(conn, out var controller))
            {
                Debug.LogWarning("[Server] Could not find match controller for connection.");
                return;
            }

            if (msg.serverDeckOperation is ServerDeckOperation.QuitMatch)
            {
                _matchService.QuitMatch(conn, controller);
                return;
            }

            if (!controller.IsCurrentPlayer(conn))
            {
                Debug.LogWarning($"[Server] Player {conn.identity.netId} acted out of turn!");
                return;
            }

            switch (msg.serverDeckOperation)
            {
                case ServerDeckOperation.PlayCard:
                    controller.HandlePlayerCard(conn, msg.Card, msg.chosenWildColor);
                    break;

                case ServerDeckOperation.DrawCard:
                    controller.HandleDrawCard(conn);
                    break;

                case ServerDeckOperation.PassTurn:
                    controller.HandlePassTurn(conn); // Draw card first, then pass turn
                    break;

                case ServerDeckOperation.CallLastCard:
                    controller.HandleLastCardCall(conn);
                    break;
            }
        }

        #endregion

        #region Client Message Handler

        [ClientCallback]
        void OnClientRoomMessage(ClientRoomMessage msg)
        {
            switch (msg.clientRoomOperation)
            {
                case ClientRoomOperation.Created:
                    OnRoomCreated(msg.roomCode);

                    _roomManager.RefreshRoomPlayers(msg.playerInfo, msg.maxPlayers);
                    _roomManager.SetRoomCode(msg.roomCode);
                    _roomManager.SetOwner(true);
                    break;

                case ClientRoomOperation.Joined:
                    OnRoomJoined(msg.roomCode);

                    _roomManager.RefreshRoomPlayers(msg.playerInfo, msg.maxPlayers);
                    _roomManager.SetRoomCode(msg.roomCode);
                    _roomManager.SetOwner(false);
                    break;

                case ClientRoomOperation.Left:
                    OnRoomLeft();
                    break;

                case ClientRoomOperation.Cancelled:
                    OnRoomLeft();
                    break;

                case ClientRoomOperation.ListUpdated:
                    _clientRepo.SetRoomList(msg.roomInfo);

                    _lobbyManager.UpdateRoomList(_clientRepo.OpenRooms);
                    break;

                case ClientRoomOperation.UpdateRoom:
                    _roomManager.RefreshRoomPlayers(msg.playerInfo, msg.maxPlayers);
                    break;

                case ClientRoomOperation.Started:
                    _uiManager.SetState(ScreenType.Game, true);
                    break;

                case ClientRoomOperation.MatchEndedByOwner:
                    OnRoomLeft();
                    break;

                case ClientRoomOperation.MatchEndedByTimeout:
                    // TODO
                    break;

                case ClientRoomOperation.Error:
                    Debug.LogError($"Room error: {msg.errorMessage}");
                    break;
            }
        }

        [ClientCallback]
        void OnClientDeckMessage(ClientDeckMessage msg)
        {
            if (CardGameController.Instance is not { } gc)
                return;

            switch (msg.clientDeckOperation)
            {
                case ClientDeckOperation.CardDealt:
                    gc.ShowDealtCards(msg.Cards, true);
                    break;

                case ClientDeckOperation.CardPlayed:
                    gc.RemoveHandCard(msg.TopDiscardCard.Id);
                    gc.RefreshHandInteractability(false);
                    break;

                case ClientDeckOperation.CardDrawn:
                    gc.ShowDealtCards(msg.Cards, false);
                    gc.OnDrawnCardReceived(msg.CanPlayDrawnCard, msg.Cards[0]);
                    break;

                case ClientDeckOperation.DeckReshuffled:
                    break;

                case ClientDeckOperation.StackedDraw:
                    break;

                case ClientDeckOperation.LastCardCalled:
                    gc.RefreshLastCardButtonInteractable();
                    break;

                case ClientDeckOperation.Error:
                    Debug.LogError($"[Deck] Error: {msg.errorMessage}");
                    gc.RefreshLastCardButtonInteractable();
                    break;
            }
        }

        #endregion

        #region Button Callbacks (UI)

        [ClientCallback]
        public void SelectRoom(Guid roomId)
        {
            if (roomId == Guid.Empty)
            {
                _clientRepo.ClearSelectedRoom();
                return;
            }

            if (!_clientRepo.OpenRooms.ContainsKey(roomId))
            {
                return;
            }

            _clientRepo.SelectRoom(roomId);

            var roomInfo = _clientRepo.OpenRooms[roomId];
        }

        #endregion

        #region UI Update Methods

        public void OnRoomCreated(Guid roomId)
        {
            _clientRepo.SetRoom(roomId, true);

            _uiManager.SetState(ScreenType.Room, true);
        }

        [ClientCallback]
        public void OnRoomJoined(Guid roomId)
        {
            _clientRepo.SetRoom(roomId, false);
            _clientRepo.ClearSelectedRoom();

            _uiManager.SetState(ScreenType.Room, true);
        }


        public void OnRoomLeft()
        {
            Debug.Log("[Client] OnRoomLeft called - returning to lobby");

            if (CardGameController.Instance != null)
            {
                Debug.Log("[Client] Destroying CardGameController instance");
                Destroy(CardGameController.Instance.gameObject);
            }

            _clientRepo.ClearRoom();

            _uiManager.SetState(ScreenType.Lobby, true);

            _lobbyManager.UpdateRoomList(_clientRepo.OpenRooms);
        }

        #endregion

        #region Helper Methods

        [ServerCallback]
        void SendRoomList(NetworkConnectionToClient conn = null)
        {
            if (conn != null)
            {
                conn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.ListUpdated,
                    roomInfo = _serverRepo.OpenRooms.Values.ToArray()
                });
            }

            else
            {
                foreach (var item in _serverRepo.WaitingConnections)
                {
                    item.Send(new ClientRoomMessage
                    {
                        clientRoomOperation = ClientRoomOperation.ListUpdated,
                        roomInfo = _serverRepo.OpenRooms.Values.ToArray()
                    });
                }
            }
        }

        #endregion
    }
}
