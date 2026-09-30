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

        public int cardCount;
        public uint connectionId;
        public string playerName;
    }

    [Serializable]
    public struct GameEndPlayerInfo
    {
        public string PlayerName;
        public int PlayerScore;
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

    #endregion

    #region Enums

    public enum ScreenType
    {
        Loading,
        Login,
        Lobby,
        RoomInfo,
        Room,
        ConnectionError,
        Game,
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

    #endregion
}