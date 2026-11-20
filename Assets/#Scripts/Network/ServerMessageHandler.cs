using Mirror;
using System;

namespace CardClash
{
    /// <summary>
    /// Routes server-side room and deck network messages to the appropriate services/controllers,
    /// including player turn authorization before processing deck operations.
    /// </summary>
    internal sealed class ServerMessageHandler
    {
        private readonly ServerRoomRepository _serverRepo;
        private readonly RoomService _roomService;
        private readonly MatchService _matchService;
        private readonly Action<NetworkConnectionToClient> _sendRoomList;

        public ServerMessageHandler(ServerRoomRepository serverRepo, RoomService roomService,
            MatchService matchService, Action<NetworkConnectionToClient> sendRoomList)
        {
            _serverRepo = serverRepo;
            _roomService = roomService;
            _matchService = matchService;
            _sendRoomList = sendRoomList;
        }

        [ServerCallback]
        internal void OnServerRoomMessage(NetworkConnectionToClient conn, ServerRoomMessage msg)
        {
            switch (msg.serverRoomOperation)
            {
                case ServerRoomOperation.Create:
                    _roomService.CreateRoom(conn, msg.maxPlayers, msg.startingCards, msg.allowStacking,
                        msg.noPowerCardFinish, msg.playSameNumber, msg.maxCardsPerTurn);
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
                    _sendRoomList(conn);
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
        internal void OnServerDeckMessage(NetworkConnectionToClient conn, ServerDeckMessage msg)
        {
            if (!TryGetMatchController(conn, out var controller))
            {
                return;
            }

            if (msg.serverDeckOperation is ServerDeckOperation.QuitMatch)
            {
                _matchService.QuitMatch(conn, controller);
                return;
            }

            if (!controller.IsCurrentPlayer(conn))
            {
                return;
            }

            switch (msg.serverDeckOperation)
            {
                case ServerDeckOperation.PlayCard:
                    controller.HandlePlayerCard(conn, msg.Card, msg.chosenWildColor);
                    break;

                case ServerDeckOperation.PlayMultipleCards:
                    controller.HandlePlayerCards(conn, msg.Cards, msg.chosenWildColor);
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
    }
}
