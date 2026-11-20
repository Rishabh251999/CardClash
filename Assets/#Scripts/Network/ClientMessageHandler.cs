using Mirror;

namespace CardClash
{
    /// <summary>
    /// Routes client-side room and deck network messages to the UI/controllers, including
    /// card-dealt, card-played, card-drawn, and error notifications.
    /// </summary>
    internal sealed class ClientMessageHandler
    {
        private readonly ClientRoomRepository _clientRepo;
        private readonly RoomManager _roomManager;
        private readonly UIManager _uiManager;
        private readonly LobbyManager _lobbyManager;
        private readonly RoomNavigationController _navigationController;

        public ClientMessageHandler(ClientRoomRepository clientRepo, RoomManager roomManager, UIManager uiManager,
            LobbyManager lobbyManager, RoomNavigationController navigationController)
        {
            _clientRepo = clientRepo;
            _roomManager = roomManager;
            _uiManager = uiManager;
            _lobbyManager = lobbyManager;
            _navigationController = navigationController;
        }

        [ClientCallback]
        internal void OnClientRoomMessage(ClientRoomMessage msg)
        {
            switch (msg.clientRoomOperation)
            {
                case ClientRoomOperation.Created:
                    _navigationController.OnRoomCreated(msg.roomCode);

                    _roomManager.SetOwner(true);
                    _roomManager.SetRoomCode(msg.roomCode);
                    _roomManager.RefreshRoomPlayers(msg.playerInfo, msg.maxPlayers);
                    _roomManager.SetHouseRules(msg.allowStacking, msg.noPowerCardFinish, msg.playSameNumber, msg.maxCardsPerTurn);
                    break;

                case ClientRoomOperation.Joined:
                    _navigationController.OnRoomJoined(msg.roomCode);

                    _roomManager.SetOwner(false);
                    _roomManager.SetRoomCode(msg.roomCode);
                    _roomManager.RefreshRoomPlayers(msg.playerInfo, msg.maxPlayers);
                    _roomManager.SetHouseRules(msg.allowStacking, msg.noPowerCardFinish, msg.playSameNumber, msg.maxCardsPerTurn);
                    break;

                case ClientRoomOperation.Left:
                    _navigationController.OnRoomLeft();
                    break;

                case ClientRoomOperation.Cancelled:
                    _navigationController.OnRoomLeft();
                    break;

                case ClientRoomOperation.ListUpdated:
                    _clientRepo.SetRoomList(msg.roomInfo);

                    _lobbyManager.UpdateRoomList(_clientRepo.OpenRooms);
                    break;

                case ClientRoomOperation.UpdateRoom:
                    _roomManager.RefreshRoomPlayers(msg.playerInfo, msg.maxPlayers);
                    break;

                case ClientRoomOperation.Started:
                    _uiManager.SetState(ScreenType.Game, false);
                    break;

                case ClientRoomOperation.MatchEndedByOwner:
                    _navigationController.OnRoomLeft();
                    break;

                case ClientRoomOperation.MatchEndedByTimeout:
                    // TODO
                    break;

                case ClientRoomOperation.Error:
                    break;
            }
        }

        [ClientCallback]
        internal void OnClientDeckMessage(ClientDeckMessage msg)
        {
            if (CardGameController.Instance is not { } gc)
                return;

            switch (msg.clientDeckOperation)
            {
                case ClientDeckOperation.CardDealt:
                    gc.ShowDealtCards(msg.Cards, true);
                    break;

                case ClientDeckOperation.CardPlayed:
                    AudioManager.Instance.PlaySoundEffect("card_play");
                    gc.RemoveHandCard(msg.TopDiscardCard.Id);
                    gc.RefreshHandInteractability(false);
                    break;

                case ClientDeckOperation.CardDrawn:
                    AudioManager.Instance.PlaySoundEffect("card_draw");
                    gc.ShowDealtCards(msg.Cards, false);
                    gc.OnDrawnCardReceived(msg.CanPlayDrawnCard, msg.Cards[0]);
                    break;

                case ClientDeckOperation.DeckReshuffled:
                    break;

                case ClientDeckOperation.StackedDraw:
                    break;

                case ClientDeckOperation.LastCardCalled:
                    AudioManager.Instance.PlaySoundEffect("last_card");
                    gc.RefreshLastCardButtonInteractable();
                    break;

                case ClientDeckOperation.Error:
                    gc.RefreshLastCardButtonInteractable();
                    break;
            }
        }
    }
}
