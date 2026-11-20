using Mirror;
using System;
using UnityEngine;

namespace CardClash
{
    /// <summary>
    /// Handles lobby/room screen navigation, including returning to the lobby after
    /// leaving a match.
    /// </summary>
    internal sealed class RoomNavigationController
    {
        private readonly ClientRoomRepository _clientRepo;
        private readonly UIManager _uiManager;
        private readonly LobbyManager _lobbyManager;

        public RoomNavigationController(ClientRoomRepository clientRepo, UIManager uiManager, LobbyManager lobbyManager)
        {
            _clientRepo = clientRepo;
            _uiManager = uiManager;
            _lobbyManager = lobbyManager;
        }

        [ClientCallback]
        internal void SelectRoom(Guid roomId)
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

        internal void OnRoomCreated(Guid roomId)
        {
            _clientRepo.SetRoom(roomId, true);

            _uiManager.SetState(ScreenType.Room, false);
        }

        [ClientCallback]
        internal void OnRoomJoined(Guid roomId)
        {
            _clientRepo.SetRoom(roomId, false);
            _clientRepo.ClearSelectedRoom();

            _uiManager.SetState(ScreenType.Room, false);
        }

        internal void OnRoomLeft()
        {
            var wasInMatch = CardGameController.Instance is { };

            if (CardGameController.Instance != null)
            {
                UnityEngine.Object.Destroy(CardGameController.Instance.gameObject);
            }

            _clientRepo.ClearRoom();

            _uiManager.SetState(ScreenType.Lobby, false);

            _lobbyManager.UpdateRoomList(_clientRepo.OpenRooms);

            if (wasInMatch)
                AudioManager.Instance.PlayTrack("menu_loop");
        }
    }
}
