using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private Button joinButton;

        #endregion

        #region Runtime State

        private readonly ServerRoomRepository _serverRepo = new();
        private readonly ClientRoomRepository _clientRepo = new();
        internal ClientRoomRepository ClientRoomRepository => _clientRepo;

        private PlayerReconnectionService _reconnectService;

        private int playerIndex = 1;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
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
            _serverRepo.PlayerInfos.TryGetValue(conn, out PlayerRoomInfo matchInfo);

            var roomCode = matchInfo.roomCode;

            // Match already started -> unchanged existing behavior
            if (_serverRepo.MatchControllers.TryGetValue(roomCode, out var matchController))
            {
                if (matchInfo.isOwner)
                    EndMatchForRoom(matchController);
                else
                    matchController.HandlePlayerQuit(conn);
            }
            else if (_serverRepo.PlayerRooms.TryGetValue(conn, out var ownedRoomCode))
            {
                // Owner disconnecting from a still-open (not started) room
                _serverRepo.PlayerRooms.Remove(conn);
                _serverRepo.OpenRooms.Remove(ownedRoomCode);

                if (_serverRepo.RoomConnections.TryGetValue(ownedRoomCode, out var connections))
                {
                    foreach (var playerConn in connections.ToList())
                    {
                        if (playerConn == conn) continue;
                        var info = _serverRepo.PlayerInfos[playerConn];
                        info.isReady = false;
                        info.roomCode = Guid.Empty;
                        _serverRepo.PlayerInfos[playerConn] = info;
                        playerConn.Send(new ClientRoomMessage { clientRoomOperation = ClientRoomOperation.Left });
                    }
                    _serverRepo.RoomConnections.Remove(ownedRoomCode);
                }
            }

            else if (roomCode != Guid.Empty)
            {
                _reconnectService.HandleDisconnect(conn, matchInfo, roomCode);
            }

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
        void OnServerRoomMessage(NetworkConnectionToClient conn, ServerRoomMessage msg)
        {
            switch (msg.serverRoomOperation)
            {
                case ServerRoomOperation.Create:
                    OnServerCreateRoom(conn, msg.maxPlayers, msg.startingCards);
                    break;
                case ServerRoomOperation.Join:
                    OnServerJoinRoom(conn, msg.roomCode);
                    break;
                case ServerRoomOperation.Leave:
                    OnServerLeaveRoom(conn);
                    break;
                case ServerRoomOperation.Cancel:
                    OnServerCancelRoom(conn);
                    break;
                case ServerRoomOperation.List:
                    SendRoomList(conn);
                    break;
                case ServerRoomOperation.Ready:
                    OnServerPlayerReady(conn, msg.roomCode);
                    break;
                case ServerRoomOperation.Start:
                    OnServerStartGame(conn);
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
                HandleQuitMatch(conn, controller);
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

        [ServerCallback]
        void OnServerCreateRoom(NetworkConnectionToClient conn, int maxPlayers, int startingCards)
        {
            if (_serverRepo.PlayerRooms.ContainsKey(conn)) return;

            var newRoomCode = Guid.NewGuid();
            _serverRepo.RoomConnections.Add(newRoomCode, new HashSet<NetworkConnectionToClient> { conn });
            _serverRepo.PlayerRooms.Add(conn, newRoomCode);
            _serverRepo.OpenRooms.Add(newRoomCode, new()
            {
                roomCode = newRoomCode,
                maxPlayers = maxPlayers,
                startingCards = startingCards,
                playerCount = 1,
                isStarted = false
            });

            var playerInfo = _serverRepo.PlayerInfos[conn];
            playerInfo.isReady = false;
            playerInfo.isOwner = true;
            playerInfo.roomCode = newRoomCode;
            _serverRepo.PlayerInfos[conn] = playerInfo;

            PlayerRoomInfo[] info = _serverRepo.RoomConnections[newRoomCode]
                .Select(playerConn => _serverRepo.PlayerInfos[playerConn])
                .ToArray();

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Created,
                roomCode = newRoomCode,
                playerInfo = info,
                maxPlayers = maxPlayers
            });

            SendRoomList();
        }

        [ServerCallback]
        void OnServerJoinRoom(NetworkConnectionToClient conn, Guid roomCode)
        {
            if (!_serverRepo.RoomConnections.ContainsKey(roomCode) || !_serverRepo.OpenRooms.ContainsKey(roomCode))
            {
                conn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.Error,
                    errorMessage = $"Room {roomCode} not found"
                });
                return;
            }

            RoomInfo roomInfo = _serverRepo.OpenRooms[roomCode];
            if (roomInfo.playerCount >= roomInfo.maxPlayers)
            {
                conn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.Error,
                    errorMessage = $"Room {roomCode} is full"
                });
                return;
            }

            roomInfo.playerCount++;
            _serverRepo.OpenRooms[roomCode] = roomInfo;
            _serverRepo.RoomConnections[roomCode].Add(conn);

            var playerInfo = _serverRepo.PlayerInfos[conn];
            playerInfo.isReady = false;
            playerInfo.roomCode = roomCode;
            playerInfo.isOwner = false;
            _serverRepo.PlayerInfos[conn] = playerInfo;

            PlayerRoomInfo[] info = _serverRepo.RoomConnections[roomCode]
                .Select(playerConn => _serverRepo.PlayerInfos[playerConn])
                .ToArray();

            SendRoomList();

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Joined,
                roomCode = roomCode,
                playerInfo = info
            });

            foreach (NetworkConnectionToClient playerConn in _serverRepo.RoomConnections[roomCode])
                playerConn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.UpdateRoom,
                    playerInfo = info,
                    maxPlayers = roomInfo.maxPlayers
                });
        }

        [ServerCallback]
        void OnServerLeaveRoom(NetworkConnectionToClient conn)
        {
            if (!_serverRepo.PlayerInfos.TryGetValue(conn, out PlayerRoomInfo playerInfo))
                return;

            var roomCode = playerInfo.roomCode;

            if (!_serverRepo.RoomConnections.ContainsKey(roomCode))
                return;

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Left
            });

            _serverRepo.RoomConnections[roomCode].Remove(conn);
            playerInfo.isReady = false;
            playerInfo.roomCode = Guid.Empty;
            _serverRepo.PlayerInfos[conn] = playerInfo;

            if (_serverRepo.RoomConnections[roomCode].Count == 0)
            {
                _serverRepo.RoomConnections.Remove(roomCode);
                _serverRepo.OpenRooms.Remove(roomCode);
                Debug.Log($"RoomManager: Room {roomCode} closed (empty)");
            }
            else
            {
                RoomInfo roomInfo = _serverRepo.OpenRooms[roomCode];
                roomInfo.playerCount = _serverRepo.RoomConnections[roomCode].Count;
                _serverRepo.OpenRooms[roomCode] = roomInfo;

                PlayerRoomInfo[] playerRoomInfo = _serverRepo.RoomConnections[roomCode]
                    .Select(playerConn => _serverRepo.PlayerInfos[playerConn])
                    .ToArray();

                foreach (NetworkConnectionToClient playerConn in _serverRepo.RoomConnections[roomCode])
                    playerConn.Send(new ClientRoomMessage
                    {
                        clientRoomOperation = ClientRoomOperation.UpdateRoom,
                        playerInfo = playerRoomInfo
                    });
            }

            SendRoomList();
        }

        [ServerCallback]
        void OnServerCancelRoom(NetworkConnectionToClient conn)
        {
            if (!_serverRepo.PlayerRooms.ContainsKey(conn)) return;

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Cancelled,
            });

            if (_serverRepo.PlayerRooms.TryGetValue(conn, out var roomCode))
            {
                _serverRepo.PlayerRooms.Remove(conn);
                _serverRepo.OpenRooms.Remove(roomCode);

                foreach (var item in _serverRepo.RoomConnections[roomCode])
                {
                    var playerInfo = _serverRepo.PlayerInfos[item];
                    playerInfo.isReady = false;
                    playerInfo.roomCode = Guid.Empty;
                    item.Send(new ClientRoomMessage
                    {
                        clientRoomOperation = ClientRoomOperation.Left
                    });
                }

                SendRoomList();
            }
        }

        [ServerCallback]
        void OnServerPlayerReady(NetworkConnectionToClient conn, Guid roomCode)
        {
            var playerInfo = _serverRepo.PlayerInfos[conn];

            playerInfo.isReady = !playerInfo.isReady;
            _serverRepo.PlayerInfos[conn] = playerInfo;

            HashSet<NetworkConnectionToClient> connections = _serverRepo.RoomConnections[roomCode];
            var info = connections.Select(playerConn => _serverRepo.PlayerInfos[playerConn]).ToArray();

            var maxPlayers = _serverRepo.OpenRooms[roomCode].maxPlayers;

            foreach (var item in _serverRepo.RoomConnections[roomCode])
            {
                item.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.UpdateRoom,
                    playerInfo = info,
                    maxPlayers = maxPlayers
                });
            }
        }

        [ServerCallback]
        private void OnServerStartGame(NetworkConnectionToClient conn)
        {
            if (!_serverRepo.PlayerRooms.TryGetValue(conn, out var roomCode))
                return;

            var startingCards = _serverRepo.OpenRooms.TryGetValue(roomCode, out var roomInfo) ? roomInfo.startingCards : 0;

            var matchController = Instantiate(matchControllerPrefab);
            if (matchController.TryGetComponent<NetworkMatch>(out var networkMatch))
            {
                networkMatch.matchId = roomCode;
            }
            NetworkServer.Spawn(matchController.gameObject);
            _serverRepo.MatchControllers[roomCode] = matchController;

            CardDeck deck = new();
            deck.BuildDeck();
            _serverRepo.RoomDecks[roomCode] = deck;

            foreach (NetworkConnectionToClient playerConn in _serverRepo.RoomConnections[roomCode])
            {
                playerConn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.Started,
                    roomCode = roomCode
                });

                var player = Instantiate(NetworkManager.singleton.playerPrefab);

                if (player.TryGetComponent<NetworkMatch>(out var playerNetworkMatch))
                {
                    playerNetworkMatch.matchId = roomCode;
                }
                else
                {
                    Debug.LogError("Player prefab does not have a NetworkMatch component.");
                }

                if (playerConn.identity != null)
                    NetworkServer.ReplacePlayerForConnection(playerConn, player, ReplacePlayerOptions.Destroy);
                else
                    NetworkServer.AddPlayerForConnection(playerConn, player);

                matchController.AddPlayer(playerConn, _serverRepo.PlayerInfos[playerConn]);

                var playerInfo = _serverRepo.PlayerInfos[playerConn];
                playerInfo.isReady = false;
                _serverRepo.PlayerInfos[playerConn] = playerInfo;
            }

            matchController.StartGame(deck, startingCards);

            _serverRepo.PlayerRooms.Remove(conn);
            _serverRepo.OpenRooms.Remove(roomCode);
            _serverRepo.RoomConnections.Remove(roomCode);

            SendRoomList();
        }

        [ServerCallback]
        private void HandleQuitMatch(NetworkConnectionToClient conn, CardGameController controller)
        {
            var isOwner = _serverRepo.PlayerInfos.TryGetValue(conn, out var playerInfo) && playerInfo.isOwner;

            Debug.Log($"[Server] HandleQuitMatch from conn {conn.connectionId}, netId={conn.identity?.netId}, isOwner={isOwner}");

            if (isOwner)
            {
                EndMatchForRoom(controller);
                return;
            }

            // Non-owner quitting an in-progress match: remove them from the match,
            // reset their room state, and send them back to the lobby individually.
            controller.HandlePlayerQuit(conn);

            if (playerInfo.roomCode != Guid.Empty &&
                _serverRepo.RoomConnections.TryGetValue(playerInfo.roomCode, out var connections))
            {
                connections.Remove(conn);

                if (_serverRepo.OpenRooms.TryGetValue(playerInfo.roomCode, out var roomInfo))
                {
                    roomInfo.playerCount = connections.Count;
                    _serverRepo.OpenRooms[playerInfo.roomCode] = roomInfo;
                }
            }

            playerInfo.isReady = false;
            playerInfo.roomCode = Guid.Empty;
            _serverRepo.PlayerInfos[conn] = playerInfo;

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Left
            });

            SendRoomList();
        }

        [ServerCallback]
        private void EndMatchForRoom(CardGameController controller)
        {
            if (!controller.TryGetComponent<NetworkMatch>(out var networkMatch))
                return;

            Guid roomCode = networkMatch.matchId;

            controller.EndMatch();

            _serverRepo.MatchControllers.Remove(roomCode);
            _serverRepo.RoomDecks.Remove(roomCode);

            if (controller != null)
                NetworkServer.Destroy(controller.gameObject);
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
                    Debug.Log("[Client] Received Left message, calling OnRoomLeft()");
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
                    _uiManager.SetState(ScreenType.Game);
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

                joinButton.interactable = false;
                return;
            }

            if (!_clientRepo.OpenRooms.ContainsKey(roomId))
            {
                joinButton.interactable = false;
                return;
            }

            _clientRepo.SelectRoom(roomId);

            var roomInfo = _clientRepo.OpenRooms[roomId];

            joinButton.interactable =
                roomInfo.playerCount < roomInfo.maxPlayers;
        }

        #endregion

        #region UI Update Methods

        public void OnRoomCreated(Guid roomId)
        {
            _clientRepo.SetRoom(roomId, true);

            _uiManager.SetState(ScreenType.Room);
        }

        [ClientCallback]
        public void OnRoomJoined(Guid roomId)
        {
            _clientRepo.SetRoom(roomId, false);
            _clientRepo.ClearSelectedRoom();

            _uiManager.SetState(ScreenType.Room);
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

            _uiManager.SetState(ScreenType.Lobby);

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
