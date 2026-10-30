using DG.Tweening;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.CullingGroup;

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

        private ScreenType _previousState;

        #endregion

        #region Unity Lifecycle 

        internal void Initialize(ClientRoomRepository clientRoomRepository) => _clientRoomRepository = clientRoomRepository;

        private void Start()
        {
            _createButton.AddListenerSafe(RequestCreateRoom);
            _settingsButton.AddListenerSafe(OnSettingsButtonClick);

            if (_uiManager is { })
                _uiManager.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            _createButton.RemoveListenerSafe(RequestCreateRoom);
            _settingsButton.RemoveListenerSafe(OnSettingsButtonClick);

            if (_uiManager is { })
                _uiManager.OnStateChanged -= OnStateChanged;
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
                Debug.Log($"Room: {roomInfo.roomCode} | Players: {roomInfo.playerCount}/{roomInfo.maxPlayers}");

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
            _uiManager.SetState(ScreenType.RoomInfo, false);
        }

        private void OnSettingsButtonClick()
        {
            SettingsIconRotation();

            _uiManager.SetState(ScreenType.Settings, false);
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

        private void SettingsIconRotation()
        {
            var transform = _settingsButton.transform.eulerAngles;

            _settingsButton.transform.DORotate(
                new Vector3(transform.x, transform.y, transform.z - 360f),
                0.25f, RotateMode.FastBeyond360).SetEase(Ease.InOutSine);
        }

        private void OnStateChanged(ScreenType state)
        {
            if (state is ScreenType.Lobby && _previousState is ScreenType.Settings)
                SettingsIconRotation();

            _previousState = state;
        }

        #endregion
    }
}