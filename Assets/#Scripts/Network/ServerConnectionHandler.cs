using Mirror;
using System;
using System.Collections;

namespace CardClash
{
    /// <summary>
    /// Handles server connection lifecycle: start/stop server, client-ready registration,
    /// disconnect handling, and player identity registration.
    /// </summary>
    internal sealed class ServerConnectionHandler
    {
        private readonly ServerRoomRepository _serverRepo;
        private readonly PlayerReconnectionService _reconnectService;
        private readonly RoomService _roomService;
        private readonly MatchService _matchService;
        private readonly ServerMessageHandler _messageHandler;
        private readonly Action _sendRoomList;

        private int _playerIndex = 1;

        public ServerConnectionHandler(ServerRoomRepository serverRepo, PlayerReconnectionService reconnectService,
            RoomService roomService, MatchService matchService, ServerMessageHandler messageHandler, Action sendRoomList)
        {
            _serverRepo = serverRepo;
            _reconnectService = reconnectService;
            _roomService = roomService;
            _matchService = matchService;
            _messageHandler = messageHandler;
            _sendRoomList = sendRoomList;
        }

        internal void InitializeServerData()
        {
            _serverRepo.Clear();
            _playerIndex = 1;
        }

        [ServerCallback]
        internal void OnStartServer()
        {
            InitializeServerData();
            NetworkServer.RegisterHandler<ServerRoomMessage>(_messageHandler.OnServerRoomMessage);
            NetworkServer.RegisterHandler<ServerDeckMessage>(_messageHandler.OnServerDeckMessage);
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
                    playerId = _playerIndex,
                    isReady = false,
                });
            }

            _sendRoomList();
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

            _sendRoomList();

            yield return null;
        }

        [ServerCallback]
        private void OnSetPlayerInfo(NetworkConnectionToClient conn, SetPlayerInfoMessage msg)
        {
            var username = msg.Username.Trim();

            if (_reconnectService.TryReconnect(conn, msg.SessionId, username))
                return;

            var info = _serverRepo.PlayerInfos.TryGetValue(conn, out var existing) ?
                existing : new() { playerId = _playerIndex += 1 };
            info.playerName = username;
            info.sessionId = msg.SessionId;
            _serverRepo.PlayerInfos[conn] = info;
        }

        [ServerCallback]
        internal void OnStopServer() => InitializeServerData();
    }
}
