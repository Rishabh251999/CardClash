using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class LobbyManager : MonoBehaviour
    {
        #region Scripts

        [SerializeField] private UIManager _uiManager;
        [SerializeField] private RoomGUI _roomPrefab;

        #endregion

        #region UI References

        [Space(5f)]
        [Header("UI References")]

        [SerializeField] private Button _createButton;
        [SerializeField] private Button _settingsButton;

        [Space(2.5f)]
        [SerializeField] private CanvasGroup _noRoomsCanvasGroup;

        #endregion

        #region Rect Transforms

        [Header("Rect Transform")]
        [Space(5f)]
        [SerializeField] private RectTransform _matchList;

        #endregion

        #region Runtime State

        private ClientRoomRepository _clientRoomRepository;

        #endregion

        #region Unity Lifecycle 

        internal void Initialize(ClientRoomRepository clientRoomRepository) => _clientRoomRepository = clientRoomRepository;

        private void Start()
        {
            _createButton.AddListenerSafe(RequestCreateRoom);
            _settingsButton.AddListenerSafe(OnSettingsButtonClick);
        }

        private void OnDestroy()
        {
            _createButton.RemoveListenerSafe(RequestCreateRoom);
            _settingsButton.RemoveListenerSafe(OnSettingsButtonClick);
        }

        #endregion

        #region Client Methods

        [ClientCallback]
        public void UpdateRoomList(IReadOnlyDictionary<Guid, RoomInfo> openRooms)
        {
            foreach (Transform child in _matchList.transform)
                Destroy(child.gameObject);

            foreach (var roomInfo in openRooms.Values)
            {
                var roomUIElement = Instantiate(_roomPrefab, _matchList.transform);
                roomUIElement.transform.SetParent(_matchList.transform, false);

                var roomCode = roomInfo.roomCode;
                roomUIElement.SetRoomInfo(roomInfo, RequestJoinRoom);
            }

            _noRoomsCanvasGroup.alpha = openRooms.Count == 0 ? 1f : 0f;
        }


        [ClientCallback]
        private void RequestCreateRoom()
        {
            _uiManager.SetState(ScreenType.RoomInfo, true);
        }

        private void OnSettingsButtonClick()
        {
            _uiManager.SetState(ScreenType.Settings, true);
        }


        [ClientCallback]
        private void RequestJoinRoom()
        {
            if (_clientRoomRepository.SelectedRoom == Guid.Empty)
            {
                Debug.LogWarning("No room selected");
                return;
            }

            NetworkClient.Send(new ServerRoomMessage
            {
                serverRoomOperation = ServerRoomOperation.Join,
                roomCode = _clientRoomRepository.SelectedRoom
            });
        }

        #endregion
    }
}