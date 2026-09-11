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
        [SerializeField] private GameManager _gameManager;
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

        #region State

        private IReadOnlyDictionary<Guid, RoomInfo> _openRooms;

        #endregion

        #region Unity Lifecycle 

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
            _openRooms = openRooms;

            // Clear existing room list UI
            foreach (Transform child in _matchList.transform)
                Destroy(child.gameObject);

            // Create UI elements for each room
            foreach (var roomInfo in openRooms.Values)
            {
                var roomUIElement = Instantiate(_roomPrefab, _matchList.transform);
                roomUIElement.transform.SetParent(_matchList.transform, false);
                roomUIElement.SetRoomInfo(roomInfo);

                if (roomUIElement.TryGetComponent<Toggle>(out var toggle))
                {
                    toggle.group = _toggleGroup;
                    toggle.onValueChanged.AddListener(_ => UpdateJoinButtonState());

                    if (roomInfo.roomCode == _gameManager.selectedRoom)
                        toggle.isOn = true;
                }
            }

            UpdateJoinButtonState();
        }

        /// <summary>
        /// Called from Create Button
        /// </summary>
        [ClientCallback]
        public void RequestCreateRoom()
        {
            _uiManager.SetState(ScreenType.RoomInfo);
        }

        /// <summary>
        /// Called from Join Button
        /// </summary>
        [ClientCallback]
        public void RequestJoinRoom()
        {
            if (_gameManager.selectedRoom == Guid.Empty)
            {
                Debug.LogWarning("No room selected");
                return;
            }

            NetworkClient.Send(new ServerRoomMessage
            {
                serverRoomOperation = ServerRoomOperation.Join,
                roomCode = _gameManager.selectedRoom
            });
        }

        [ClientCallback]
        private void UpdateJoinButtonState()
        {
            var canJoin = _gameManager.selectedRoom != Guid.Empty && 
                _openRooms != null && _openRooms.TryGetValue(_gameManager.selectedRoom, out var roomInfo)
                && roomInfo.playerCount < roomInfo.maxPlayers;

            _joinButton.interactable = canJoin;
        }

        #endregion
    }
}