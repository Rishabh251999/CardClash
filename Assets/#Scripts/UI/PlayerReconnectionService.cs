using Mirror;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace CardClash
{
    internal sealed class PlayerReconnectionService
    {
        private readonly ServerRoomRepository _repo;
        private readonly MonoBehaviour _coroutineRunner;
        private readonly Action _sendRoomList;

        private const float RoomReconnectGraceSeconds = 60f;

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

            var allInfo = connections.Select(c => _repo.PlayerInfos[c]).ToArray();

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
}
