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
        [SerializeField] private Button _joinButton;

        [Space(2.5f)]

        [SerializeField] private ToggleGroup _toggleGroup;

        [Space(2.5f)]
        [SerializeField] private RectTransform _matchList;

        #endregion

        #region Runtime State

        private ClientRoomRepository _clientRoomRepository;

        #endregion

        #region Unity Lifecycle 
        internal void Initialize(ClientRoomRepository clientRoomRepository) => _clientRoomRepository = clientRoomRepository;

        private void Start()
        {
            _createButton.onClick.AddListener(RequestCreateRoom);
            _joinButton.onClick.AddListener(RequestJoinRoom);

            _joinButton.interactable = false;
        }

        private void OnDestroy()
        {
            _createButton.onClick.RemoveListener(RequestCreateRoom);
            _joinButton.onClick.RemoveListener(RequestJoinRoom);
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
                roomUIElement.SetRoomInfo(roomInfo);

                if (roomUIElement.TryGetComponent<Toggle>(out var toggle))
                {
                    toggle.group = _toggleGroup;
                    toggle.onValueChanged.AddListener(_ => UpdateJoinButtonState());

                    if (roomInfo.roomCode == _clientRoomRepository.SelectedRoom)
                        toggle.isOn = true;
                }
            }

            UpdateJoinButtonState();
        }


        [ClientCallback]
        public void RequestCreateRoom()
        {
            _uiManager.SetState(ScreenType.RoomInfo);
        }


        [ClientCallback]
        public void RequestJoinRoom()
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


        [ClientCallback]
        private void UpdateJoinButtonState()
        {
            var selectedRoom = _clientRoomRepository.SelectedRoom;

            var canJoin = selectedRoom != Guid.Empty && 
                _clientRoomRepository.OpenRooms.TryGetValue(selectedRoom,out var roomInfo) && 
                roomInfo.playerCount < roomInfo.maxPlayers;

            _joinButton.interactable = canJoin;
        }

        #endregion
    }
}