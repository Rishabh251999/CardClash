using Mirror;
using System;
using System.Linq;

namespace CardClash
{
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

        internal void CreateRoom(NetworkConnectionToClient conn, int maxPlayers, int startingCards, bool allowStacking, bool noPowerCardFinish, 
            bool playSameNumber, int maxCardsPerTurn)
        {
            if (conn == null || !_repo.PlayerInfos.TryGetValue(conn, out var playerInfo))
                return;

            if (_repo.PlayerRooms.ContainsKey(conn))
                return;

            if (maxPlayers < 2 || startingCards < 1)
            {
                SendError(conn, "Invalid room settings.");
                return;
            }

            var roomCode = Guid.NewGuid();

            _repo.RoomConnections.Add(roomCode, new() { conn });

            _repo.PlayerRooms.Add(conn, roomCode);

            _repo.OpenRooms.Add(roomCode, new RoomInfo
            {
                roomCode = roomCode,
                maxPlayers = maxPlayers,
                startingCards = startingCards,
                playerCount = 1,
                isStarted = false,
                allowStacking = allowStacking,
                noPowerCardFinish = noPowerCardFinish,
                playSameNumber = playSameNumber,
                maxCardsPerTurn = maxCardsPerTurn
            });

            playerInfo.isReady = true;
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

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Created,
                roomCode = roomCode,
                playerInfo = players,
                maxPlayers = maxPlayers,
                allowStacking = allowStacking,
                noPowerCardFinish = noPowerCardFinish,
                playSameNumber = playSameNumber,
                maxCardsPerTurn = maxCardsPerTurn
            });

            _sendRoomList?.Invoke();
        }

        #endregion

        #region Join Room

        internal void JoinRoom(NetworkConnectionToClient conn, Guid roomCode)
        {
            if (conn == null || !_repo.PlayerInfos.TryGetValue(conn, out var playerInfo))
                return;

            if (_repo.PlayerRooms.ContainsKey(conn))
            {
                SendError(conn, "You are already in a room.");
                return;
            }

            if (!_repo.RoomConnections.TryGetValue(roomCode, out var connections) ||
                !_repo.OpenRooms.TryGetValue(roomCode, out var roomInfo))
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

            playerInfo.isReady = true;
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
                maxPlayers = roomInfo.maxPlayers,
                allowStacking = roomInfo.allowStacking,
                noPowerCardFinish = roomInfo.noPowerCardFinish,
                playSameNumber = roomInfo.playSameNumber,
                maxCardsPerTurn = roomInfo.maxCardsPerTurn
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
}
