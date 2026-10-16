using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardClash
{
    #region Network Messages

    /// <summary>
    /// Message sent from client to server for room operations
    /// </summary>
    public struct ServerRoomMessage : NetworkMessage
    {
        public ServerRoomOperation serverRoomOperation;
        public Guid roomCode;

        public int maxPlayers;
        public int startingCards;
    }

    public struct ServerDeckMessage : NetworkMessage
    {
        public ServerDeckOperation serverDeckOperation;
        public Guid RoomCode;
        public GameCard Card;
        public CardColor chosenWildColor;
    }

    /// <summary>
    /// Message sent from server to client
    /// </summary>
    public struct ClientRoomMessage : NetworkMessage
    {
        public ClientRoomOperation clientRoomOperation;
        public Guid roomCode;

        public string errorMessage;

        public int maxPlayers;

        public RoomInfo[] roomInfo;
        public PlayerRoomInfo[] playerInfo;
    }

    public struct ClientDeckMessage : NetworkMessage
    {
        public ClientDeckOperation clientDeckOperation;
        public Guid RoomCode;
        public GameCard[] Cards;
        public GameCard TopDiscardCard;
        public int DrawPileCount;
        public string errorMessage;
        public bool CanPlayDrawnCard;
    }

    /// <summary>
    /// Information about a room
    /// </summary>
    [Serializable]
    public struct RoomInfo
    {
        public Guid roomCode;
        public int playerCount;
        public int maxPlayers;
        public int startingCards;
        public bool isStarted;
    }

    /// <summary>
    /// Information about a player in a room
    /// </summary>
    [Serializable]
    public struct PlayerRoomInfo
    {
        public int avatarId;
        public int playerId;
        public int cardCount;

        public bool isReady;
        public bool isOwner;

        public Guid roomCode;

        public string playerName;
        public string sessionId;
    }

    [Serializable]
    public struct PlayerGameInfo
    {
        public bool isOwner;
        public bool hasCalledLastCard;

        public int avatarId;
        public int cardCount;
        public uint connectionId;

        public string playerName;
    }

    [Serializable]
    public struct GameEndPlayerInfo
    {
        public string PlayerName;
        public int PlayerScore;
        public int AvatarId;
    }

    /// <summary>
    /// Operations the server can perform on rooms
    /// </summary>
    public enum ServerRoomOperation : byte
    {
        None,
        Create,
        Join,
        Leave,
        List,
        Start,
        Ready,
        Cancel
    }

    public enum ServerDeckOperation : byte
    {
        None = 0,
        DrawCard = 1,  
        PlayCard = 2, 
        PassTurn = 3,
        QuitMatch = 4,
        CallLastCard = 5,
    }

    /// <summary>
    /// Operations the client receives about rooms
    /// </summary>
    public enum ClientRoomOperation : byte
    {
        None,
        Created,
        Joined,
        Left,
        Cancelled,
        UpdateRoom,
        ListUpdated,
        Started,
        MatchEndedByOwner, 
        MatchEndedByTimeout,
        Error
    }

    public enum ClientDeckOperation : byte
    {
        None = 0,
        DeckCreated = 1,
        CardDealt = 2,
        CardPlayed = 3,
        CardDrawn = 4,
        DeckReshuffled = 5,
        Error = 6,
        StackedDraw = 7,
        PlayerQuit = 8,
        LastCardCalled = 9,   
        LastCardPenalty = 10,
    }

    #endregion

    #region Card

    public struct CardData
    {
        public string name;
        public byte value;
        public Texture sprite;
    }

    public struct PlayerCardData
    {
        public byte cardId;

        [NonSerialized]
        public GameObject clientCardObject;
    }

    public class CustomSyncDictionary<TKey, TValue> : Mirror.SyncIDictionary<TKey, TValue>
    {
        public CustomSyncDictionary(IDictionary<TKey, TValue> objects) : base(objects) 
        { 

        }

        public bool SetLocalValue(TKey key, TValue value)
        {
            if (ContainsKey(key))
            {
                objects[key] = value;
                return true;
            }
            return false;
        }
    }

    public enum CardColor : byte
    {
        None = 0,
        Red = 1,
        Yellow = 2,
        Green = 3,
        Blue = 4,
    }

    public enum CardType : byte
    {
        Number = 0,
        Skip = 1,
        Reverse = 2,
        DrawTwo = 3,
        Wild = 4,
        WildDrawFour = 5
    }

    [Serializable]
    public struct GameCard
    {
        public byte Id;

        public CardColor Color;

        public CardType Type;

        public byte FaceValue;

        public override readonly string ToString()
        {
            string colorStr = Color == CardColor.None ? "" : $"{Color} ";
            string typeStr = Type == CardType.Number
                ? FaceValue.ToString()
                : TypeToString(Type);
            return $"{colorStr}{typeStr}";
        }

        private readonly string TypeToString(CardType type) => type switch
        {
            CardType.Skip => "Skip",
            CardType.Reverse => "Reverse",
            CardType.DrawTwo => "Draw",
            CardType.Wild => "Wild",
            CardType.WildDrawFour => "WildDrawFour",
            _ => "Unknown"
        };
    }

    #endregion

    #region struct

    [Serializable]
    public struct PanelEntry
    {
        public ScreenType ScreenType;
        public CanvasGroup CanvasGroup;
    }

    public struct SetPlayerInfoMessage : NetworkMessage
    {
        public string Username;
        public string SessionId;
    }

    [Serializable]
    public struct PendingDrawState
    {
        public int Count;
        public CardType Type;
    }

    #endregion

    #region Enums

    public enum ScreenType
    {
        Room,
        Game,
        Login,
        Lobby,
        Profile,
        Loading,
        RoomInfo,
        ConnectionError,
    }

    public enum NotificationType
    {
        Red, 
        Blue,
        Green,
        Yellow,
        LastCard
    }

    #endregion

    #region Classes

    internal sealed class ServerRoomRepository
    {
        internal readonly Dictionary<Guid, RoomInfo> OpenRooms = new();
        internal readonly Dictionary<Guid, CardDeck> RoomDecks = new();
        internal readonly Dictionary<Guid, CardGameController> MatchControllers = new();
        internal readonly Dictionary<NetworkConnectionToClient, Guid> PlayerRooms = new();
        internal readonly Dictionary<NetworkConnectionToClient, PlayerRoomInfo> PlayerInfos = new();
        internal readonly Dictionary<Guid, HashSet<NetworkConnectionToClient>> RoomConnections = new();
        internal readonly Dictionary<string, (Guid roomCode, PlayerRoomInfo info)> DisconnectedRoomPlayers = new();

        internal readonly List<NetworkConnectionToClient> WaitingConnections = new();

        internal void Clear()
        {
            PlayerRooms.Clear();
            OpenRooms.Clear();
            RoomConnections.Clear();
            WaitingConnections.Clear();
            RoomDecks.Clear();
            MatchControllers.Clear();
            DisconnectedRoomPlayers.Clear();
        }
    }

    internal sealed class ClientRoomRepository
    {
        internal readonly Dictionary<Guid, RoomInfo> OpenRooms = new();

        internal Guid CurrentRoom { get; private set; } = Guid.Empty;

        internal Guid SelectedRoom { get; private set; } = Guid.Empty;

        internal bool IsOwner { get; private set; }

        internal void SetRoom(Guid roomCode, bool isOwner)
        {
            CurrentRoom = roomCode;
            IsOwner = isOwner;
        }

        internal void ClearRoom()
        {
            CurrentRoom = Guid.Empty;
            IsOwner = false;
        }

        internal void SelectRoom(Guid roomCode) => SelectedRoom = roomCode;

        internal void ClearSelectedRoom() => SelectedRoom = Guid.Empty;

        internal void SetRoomList(RoomInfo[] rooms)
        {
            OpenRooms.Clear();

            foreach (var room in rooms)
                OpenRooms[room.roomCode] = room;
        }

        internal void Clear()
        {
            OpenRooms.Clear();
            CurrentRoom = Guid.Empty;
            SelectedRoom = Guid.Empty;
            IsOwner = false;
        }
    }

    internal class PlayerReconnectionService
    {
        private readonly ServerRoomRepository _repo;
        private readonly MonoBehaviour _coroutineRunner;
        private readonly Action _sendRoomList;

        private const float RoomReconnectGraceSeconds = 30f;

        public PlayerReconnectionService(ServerRoomRepository repo, MonoBehaviour coroutineRunner, Action sendRoomList)
        {
            _repo = repo;
            _coroutineRunner = coroutineRunner;
            _sendRoomList = sendRoomList;
        }

        /// <summary>
        /// Called from OnServerDisconnect when a non-owner disconnects from an open room.
        /// Starts a grace period during which the player can reconnect to the same seat.
        /// </summary>
        internal void HandleDisconnect(NetworkConnectionToClient conn, PlayerRoomInfo matchInfo, Guid roomCode)
        {
            if (string.IsNullOrEmpty(matchInfo.sessionId))
            {
                RemovePlayerFromRoom(conn, roomCode);
                return;
            }

            // Remove the dead connection now, but preserve their seat info for reconnection
            if (_repo.RoomConnections.TryGetValue(roomCode, out var conns))
                conns.Remove(conn);

            _repo.DisconnectedRoomPlayers[matchInfo.sessionId] = (roomCode, matchInfo);
            _coroutineRunner.StartCoroutine(ExpireRoomReconnectWindow(matchInfo.sessionId, roomCode));
        }

        /// <summary>
        /// Called from OnSetPlayerInfo. Returns true if this session was reconnected to its previous room.
        /// </summary>
        internal bool TryReconnect(NetworkConnectionToClient conn, string sessionId, string username)
        {
            if (string.IsNullOrEmpty(sessionId) || !_repo.DisconnectedRoomPlayers.TryGetValue(sessionId, out var entry))
                return false;

            _repo.DisconnectedRoomPlayers.Remove(sessionId);
            ReconnectPlayerToRoom(conn, entry.roomCode, entry.info, username, sessionId);
            return true;
        }

        private IEnumerator ExpireRoomReconnectWindow(string sessionId, Guid roomCode)
        {
            yield return new WaitForSeconds(RoomReconnectGraceSeconds);

            if (_repo.DisconnectedRoomPlayers.TryGetValue(sessionId, out var entry) && entry.roomCode == roomCode)
            {
                _repo.DisconnectedRoomPlayers.Remove(sessionId);
                // Player never reconnected in time -> remove from room now
                RemovePlayerFromRoomBySession(roomCode, entry.info);
            }
        }

        private void RemovePlayerFromRoom(NetworkConnectionToClient conn, Guid roomCode)
        {
            if (!_repo.RoomConnections.TryGetValue(roomCode, out var connections))
                return;

            connections.Remove(conn);

            if (_repo.OpenRooms.TryGetValue(roomCode, out RoomInfo roomInfo))
            {
                roomInfo.playerCount = connections.Count;
                _repo.OpenRooms[roomCode] = roomInfo;
            }

            if (connections.Count == 0)
            {
                _repo.RoomConnections.Remove(roomCode);
                _repo.OpenRooms.Remove(roomCode);
                _sendRoomList();
                return;
            }

            var playerInfoArr = connections
                .Where(_repo.PlayerInfos.ContainsKey)
                .Select(c => _repo.PlayerInfos[c])
                .ToArray();

            foreach (var playerConn in connections)
                playerConn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.UpdateRoom,
                    playerInfo = playerInfoArr
                });

            _sendRoomList();
        }

        private void RemovePlayerFromRoomBySession(Guid roomCode, PlayerRoomInfo info)
        {
            if (!_repo.RoomConnections.TryGetValue(roomCode, out var connections))
                return;

            if (_repo.OpenRooms.TryGetValue(roomCode, out RoomInfo roomInfo))
            {
                roomInfo.playerCount = connections.Count; // recompute from actual set, safer than --
                _repo.OpenRooms[roomCode] = roomInfo;
            }

            if (connections.Count == 0)
            {
                _repo.RoomConnections.Remove(roomCode);
                _repo.OpenRooms.Remove(roomCode);
                return;
            }

            var playerInfoArr = connections.Where(_repo.PlayerInfos.ContainsKey).Select(c => _repo.PlayerInfos[c]).ToArray();
            foreach (var playerConn in connections)
                playerConn.Send(new ClientRoomMessage { clientRoomOperation = ClientRoomOperation.UpdateRoom, playerInfo = playerInfoArr });

            _sendRoomList();
        }

        private void ReconnectPlayerToRoom(NetworkConnectionToClient conn, Guid roomCode, PlayerRoomInfo oldInfo, string username, string sessionId)
        {
            if (!_repo.RoomConnections.TryGetValue(roomCode, out var connections) || !_repo.OpenRooms.ContainsKey(roomCode))
            {
                // Room no longer exists (owner cancelled, etc.) -> fall back to normal lobby state
                var info = _repo.PlayerInfos.TryGetValue(conn, out var existing) ? existing : new PlayerRoomInfo();
                info.playerName = username;
                info.sessionId = sessionId;
                _repo.PlayerInfos[conn] = info;
                return;
            }

            oldInfo.playerName = username;
            oldInfo.sessionId = sessionId;
            oldInfo.isReady = false;
            _repo.PlayerInfos[conn] = oldInfo;
            connections.Add(conn);

            if (_repo.OpenRooms.TryGetValue(roomCode, out var roomInfo))
            {
                roomInfo.playerCount = connections.Count;
                _repo.OpenRooms[roomCode] = roomInfo;
            }

            PlayerRoomInfo[] allInfo = connections.Select(c => _repo.PlayerInfos[c]).ToArray();

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Joined,
                roomCode = roomCode,
                playerInfo = allInfo
            });

            foreach (var playerConn in connections)
                playerConn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.UpdateRoom,
                    playerInfo = allInfo,
                    maxPlayers = _repo.OpenRooms[roomCode].maxPlayers
                });

            _sendRoomList();
        }
    }

    internal sealed class RoomService
    {
        private readonly ServerRoomRepository _repo;
        private readonly Action _sendRoomList;

        public RoomService(ServerRoomRepository repo, Action sendRoomList)
        {
            _repo = repo;
            _sendRoomList = sendRoomList;
        }

        #region Create Room

        internal void CreateRoom(
            NetworkConnectionToClient conn,
            int maxPlayers,
            int startingCards)
        {
            if (conn == null ||
                !_repo.PlayerInfos.TryGetValue(conn, out var playerInfo))
                return;

            if (_repo.PlayerRooms.ContainsKey(conn))
                return;

            if (maxPlayers < 2 || startingCards < 1)
            {
                SendError(conn, "Invalid room settings.");
                return;
            }

            var roomCode = Guid.NewGuid();

            _repo.RoomConnections.Add(
                roomCode,
                new() { conn });

            _repo.PlayerRooms.Add(conn, roomCode);

            _repo.OpenRooms.Add(roomCode, new RoomInfo
            {
                roomCode = roomCode,
                maxPlayers = maxPlayers,
                startingCards = startingCards,
                playerCount = 1,
                isStarted = false
            });

            playerInfo.isReady = false;
            playerInfo.isOwner = true;
            playerInfo.roomCode = roomCode;
            playerInfo.avatarId = 1;

            _repo.PlayerInfos[conn] = playerInfo;

            var players = GetRoomPlayers(roomCode);

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Created,
                roomCode = roomCode,
                playerInfo = players,
                maxPlayers = maxPlayers
            });

            _sendRoomList?.Invoke();
        }

        #endregion

        #region Join Room

        internal void JoinRoom(
            NetworkConnectionToClient conn,
            Guid roomCode)
        {
            if (conn == null ||
                !_repo.PlayerInfos.TryGetValue(conn, out var playerInfo))
                return;

            if (_repo.PlayerRooms.ContainsKey(conn))
            {
                SendError(conn, "You are already in a room.");
                return;
            }

            if (!_repo.RoomConnections.TryGetValue(
                    roomCode, out var connections) ||
                !_repo.OpenRooms.TryGetValue(
                    roomCode, out var roomInfo))
            {
                SendError(conn, $"Room {roomCode} not found.");
                return;
            }

            if (connections.Count >= roomInfo.maxPlayers)
            {
                SendError(conn, $"Room {roomCode} is full.");
                return;
            }

            connections.Add(conn);
            _repo.PlayerRooms[conn] = roomCode;

            playerInfo.isReady = false;
            playerInfo.isOwner = false;
            playerInfo.roomCode = roomCode;
            playerInfo.avatarId = connections.Count;

            _repo.PlayerInfos[conn] = playerInfo;

            roomInfo.playerCount = connections.Count;
            _repo.OpenRooms[roomCode] = roomInfo;

            var players = GetRoomPlayers(roomCode);

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Joined,
                roomCode = roomCode,
                playerInfo = players,
                maxPlayers = roomInfo.maxPlayers
            });

            BroadcastRoomUpdate(roomCode);
            _sendRoomList?.Invoke();
        }

        #endregion

        #region Leave Room

        internal void LeaveRoom(NetworkConnectionToClient conn)
        {
            if (conn == null ||
                !_repo.PlayerInfos.TryGetValue(conn, out var playerInfo))
                return;

            if (playerInfo.isOwner)
            {
                CancelRoom(conn);
                return;
            }

            var roomCode = playerInfo.roomCode;

            if (roomCode == Guid.Empty ||
                !_repo.RoomConnections.TryGetValue(
                    roomCode, out var connections))
                return;

            connections.Remove(conn);
            _repo.PlayerRooms.Remove(conn);

            playerInfo.isReady = false;
            playerInfo.isOwner = false;
            playerInfo.roomCode = Guid.Empty;

            _repo.PlayerInfos[conn] = playerInfo;

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Left
            });

            if (connections.Count == 0)
            {
                _repo.RoomConnections.Remove(roomCode);
                _repo.OpenRooms.Remove(roomCode);
            }
            else
            {
                if (_repo.OpenRooms.TryGetValue(
                        roomCode, out var roomInfo))
                {
                    roomInfo.playerCount = connections.Count;
                    _repo.OpenRooms[roomCode] = roomInfo;
                }

                BroadcastRoomUpdate(roomCode);
            }

            _sendRoomList?.Invoke();
        }

        #endregion

        #region Cancel Room

        internal void CancelRoom(NetworkConnectionToClient conn)
        {
            if (conn == null ||
                !_repo.PlayerInfos.TryGetValue(conn, out var ownerInfo) ||
                !ownerInfo.isOwner)
                return;

            if (!_repo.PlayerRooms.TryGetValue(conn, out var roomCode) ||
                !_repo.RoomConnections.TryGetValue(
                    roomCode, out var connections))
                return;

            // Remove all waiting-room records.
            _repo.RoomConnections.Remove(roomCode);
            _repo.OpenRooms.Remove(roomCode);

            foreach (var playerConn in connections.ToArray())
            {
                if (!_repo.PlayerInfos.TryGetValue(
                        playerConn, out var playerInfo))
                    continue;

                playerInfo.isReady = false;
                playerInfo.isOwner = false;
                playerInfo.roomCode = Guid.Empty;

                _repo.PlayerInfos[playerConn] = playerInfo;
                _repo.PlayerRooms.Remove(playerConn);

                playerConn.Send(new ClientRoomMessage
                {
                    clientRoomOperation =
                        playerConn == conn
                            ? ClientRoomOperation.Cancelled
                            : ClientRoomOperation.Left
                });
            }

            _sendRoomList?.Invoke();
        }

        #endregion

        #region Ready

        internal void ToggleReady(
            NetworkConnectionToClient conn,
            Guid roomCode)
        {
            if (conn == null ||
                !_repo.PlayerInfos.TryGetValue(conn, out var playerInfo))
                return;

            // The player must belong to the specified room.
            if (playerInfo.roomCode != roomCode ||
                !_repo.RoomConnections.TryGetValue(
                    roomCode, out var connections) ||
                !connections.Contains(conn) ||
                !_repo.OpenRooms.TryGetValue(
                    roomCode, out var roomInfo))
                return;

            playerInfo.isReady = !playerInfo.isReady;
            _repo.PlayerInfos[conn] = playerInfo;

            BroadcastRoomUpdate(roomCode);
        }

        #endregion

        #region Helpers

        private PlayerRoomInfo[] GetRoomPlayers(Guid roomCode)
        {
            if (!_repo.RoomConnections.TryGetValue(
                    roomCode, out var connections))
                return Array.Empty<PlayerRoomInfo>();

            return connections
                .Where(c => _repo.PlayerInfos.ContainsKey(c))
                .Select(c => _repo.PlayerInfos[c])
                .ToArray();
        }

        private void BroadcastRoomUpdate(Guid roomCode)
        {
            if (!_repo.RoomConnections.TryGetValue(
                    roomCode, out var connections))
                return;

            var players = GetRoomPlayers(roomCode);

            _repo.OpenRooms.TryGetValue(
                roomCode, out var roomInfo);

            foreach (var playerConn in connections.ToArray())
            {
                playerConn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.UpdateRoom,
                    playerInfo = players,
                    maxPlayers = roomInfo.maxPlayers
                });
            }
        }

        private void SendError(NetworkConnectionToClient conn, string message)
        {
            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Error,
                errorMessage = message
            });
        }

        #endregion
    }

    internal sealed class MatchService
    {
        private readonly ServerRoomRepository _repo;
        private readonly CardGameController _matchControllerPrefab;
        private readonly System.Action _sendRoomList;

        public MatchService(
            ServerRoomRepository repo,
            CardGameController matchControllerPrefab,
            System.Action sendRoomList)
        {
            _repo = repo;
            _matchControllerPrefab = matchControllerPrefab;
            _sendRoomList = sendRoomList;
        }

        #region Start Match

        internal void StartMatch(NetworkConnectionToClient conn)
        {
            if (!NetworkServer.active || conn == null)
                return;

            // Only the room owner can start a match.
            if (!_repo.PlayerInfos.TryGetValue(conn, out var ownerInfo) ||
                !ownerInfo.isOwner)
                return;

            if (!_repo.PlayerRooms.TryGetValue(conn, out var roomCode))
                return;

            if (!_repo.OpenRooms.TryGetValue(roomCode, out var roomInfo) ||
                !_repo.RoomConnections.TryGetValue(
                    roomCode, out var connections))
                return;

            var players = connections.ToArray();

            // Validate room state before spawning network objects.
            if (players.Length < 2 ||
                players.Length > roomInfo.maxPlayers)
                return;

            if (players.Any(player =>
                    player == null ||
                    !_repo.PlayerInfos.TryGetValue(
                        player, out var info) ||
                    info.roomCode != roomCode))
            {
                Debug.LogWarning(
                    $"[Server] Invalid player data in room {roomCode}.");
                return;
            }

            if (players.Any(player =>
                    !_repo.PlayerInfos[player].isReady))
            {
                Debug.LogWarning(
                    $"[Server] Cannot start room {roomCode}: " +
                    "not all players are ready.");
                return;
            }

            if (_matchControllerPrefab == null ||
                NetworkManager.singleton == null ||
                NetworkManager.singleton.playerPrefab == null)
            {
                Debug.LogError(
                    "[Server] Match controller or player prefab is missing.");
                return;
            }

            // Validate required NetworkMatch components before spawning.
            if (!_matchControllerPrefab.TryGetComponent<NetworkMatch>(
                    out _))
            {
                Debug.LogError(
                    "[Server] Match controller prefab requires NetworkMatch.");
                return;
            }

            if (!NetworkManager.singleton.playerPrefab.TryGetComponent<NetworkMatch>(
                    out _))
            {
                Debug.LogError(
                    "[Server] Player prefab requires NetworkMatch.");
                return;
            }

            CardGameController matchController = null;

            try
            {
                // Create the match controller.
                matchController = UnityEngine.Object.Instantiate(
                    _matchControllerPrefab);

                matchController.GetComponent<NetworkMatch>().matchId =
                    roomCode;

                NetworkServer.Spawn(matchController.gameObject);

                _repo.MatchControllers.Add(
                    roomCode, matchController);

                // Build the deck for this match.
                CardDeck deck = new();
                deck.BuildDeck();

                _repo.RoomDecks.Add(roomCode, deck);

                // Create a match player for every connection.
                foreach (var playerConn in players)
                {
                    var player = UnityEngine.Object.Instantiate(NetworkManager.singleton.playerPrefab);

                    player.GetComponent<NetworkMatch>().matchId = roomCode;

                    if (playerConn.identity != null)
                    {
                        NetworkServer.ReplacePlayerForConnection(
                            playerConn,
                            player,
                            ReplacePlayerOptions.Destroy);
                    }
                    else
                    {
                        NetworkServer.AddPlayerForConnection(
                            playerConn,
                            player);
                    }

                    matchController.AddPlayer(
                        playerConn,
                        _repo.PlayerInfos[playerConn]);

                    var info = _repo.PlayerInfos[playerConn];
                    info.isReady = false;
                    _repo.PlayerInfos[playerConn] = info;

                    playerConn.Send(new ClientRoomMessage
                    {
                        clientRoomOperation = ClientRoomOperation.Started,
                        roomCode = roomCode
                    });
                }

                // Start gameplay after registering every player.
                matchController.StartGame(
                    deck, roomInfo.startingCards);

                // Remove all waiting-room mappings.
                foreach (var playerConn in players)
                    _repo.PlayerRooms.Remove(playerConn);

                _repo.OpenRooms.Remove(roomCode);
                _repo.RoomConnections.Remove(roomCode);

                _sendRoomList?.Invoke();

                Debug.Log(
                    $"[Server] Match started for room {roomCode}.");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);

                // Avoid retaining a partially registered match.
                _repo.MatchControllers.Remove(roomCode);
                _repo.RoomDecks.Remove(roomCode);

                if (matchController != null)
                {
                    matchController.EndMatch();

                    if (matchController.gameObject != null)
                        NetworkServer.Destroy(matchController.gameObject);
                }

                // Do not remove the waiting-room records here:
                // they may be needed to recover from a failed startup.
            }
        }

        #endregion

        #region Quit Match

        internal void QuitMatch(NetworkConnectionToClient conn, CardGameController controller)
        {
            if (!NetworkServer.active ||
                conn == null ||
                controller == null)
                return;

            if (!_repo.PlayerInfos.TryGetValue(
                    conn, out var playerInfo))
                return;

            Debug.Log(
                $"[Server] Player quitting match. " +
                $"Connection={conn.connectionId}, " +
                $"Owner={playerInfo.isOwner}");

            // Owner quitting ends the match for everyone.
            if (playerInfo.isOwner)
            {
                EndMatch(controller);
                return;
            }

            // Remove the player from the active game.
            controller.HandlePlayerQuit(conn);

            playerInfo.isReady = false;
            playerInfo.roomCode = System.Guid.Empty;
            _repo.PlayerInfos[conn] = playerInfo;

            _repo.PlayerRooms.Remove(conn);

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Left
            });

            _sendRoomList?.Invoke();
        }

        #endregion

        #region End Match

        internal void EndMatch(CardGameController controller)
        {
            if (!NetworkServer.active || controller == null)
                return;

            if (!controller.TryGetComponent<NetworkMatch>(
                    out var networkMatch))
            {
                Debug.LogWarning(
                    "[Server] Cannot end match: NetworkMatch is missing.");
                return;
            }

            var roomCode = networkMatch.matchId;

            Debug.Log($"[Server] Ending match for room {roomCode}.");

            controller.EndMatch();

            _repo.MatchControllers.Remove(roomCode);
            _repo.RoomDecks.Remove(roomCode);

            if (controller != null)
                NetworkServer.Destroy(controller.gameObject);

            _sendRoomList?.Invoke();
        }

        #endregion
    }

    #endregion
}