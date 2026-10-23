using Mirror;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class RoomManager : MonoBehaviour
    {
        #region Constants/Readonly

        private readonly string _playerJoinedSound = "player_join";
        private readonly string _playerLeftSound = "player_leave";

        #endregion

        #region Scripts

        [Header("Script References")]

        [SerializeField] private PlayerGUI _playerPrefab;

        #endregion

        #region UI References

        [Space(5)]
        [Header("GUI References")]
        [SerializeField] private TextMeshProUGUI _roomCodeText;
        [SerializeField] private TextMeshProUGUI _playerCountText;

        [Space(5)]

        [SerializeField] private Button _readyButton;

        private Button _startButton;
        private Button _leaveButton;
        private Button _cancelButton;

        #endregion

        #region GameObject References

        [Space(5)]
        [Header("GameObject References")]

        [SerializeField] private GameObject _playerList;
        [SerializeField] private GameObject _leaveGameObject;
        [SerializeField] private GameObject _cancelGameObject;
        [SerializeField] private GameObject _startButtonGameObject;

        #endregion

        #region Runtime State

        private bool _owner;

        private Guid _roomCode = Guid.Empty;

        private int _previousPlayerCount = -1;

        private readonly List<PlayerGUI> _slots = new();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _startButton = _startButtonGameObject.GetComponent<Button>();
            _cancelButton = _cancelGameObject.GetComponent<Button>();
            _leaveButton = _leaveGameObject.GetComponent<Button>();
        }

        private void Start()
        {
            _startButton.AddListenerSafe(OnStartButtonClicked);
            _readyButton.AddListenerSafe(OnReadyButtonClicked);
            _cancelButton.AddListenerSafe(OnCancelButtonClicked);
            _leaveButton.AddListenerSafe(OnLeaveButtonClicked);
        }

        private void OnDestroy()
        {
            _startButton.RemoveListenerSafe(OnStartButtonClicked);
            _readyButton.RemoveListenerSafe(OnReadyButtonClicked);
            _cancelButton.RemoveListenerSafe(OnCancelButtonClicked);
            _leaveButton.RemoveListenerSafe(OnLeaveButtonClicked);
        }

        private void OnStartButtonClicked()
        {
            if (!_owner || _roomCode == Guid.Empty) return;

            NetworkClient.Send(new ServerRoomMessage
            {
                serverRoomOperation = ServerRoomOperation.Start,
            });
        }

        private void OnReadyButtonClicked()
        {
            if (_roomCode == Guid.Empty)
            {
                Debug.LogWarning("Not in a room");
                return;
            }

            NetworkClient.Send(new ServerRoomMessage
            {
                serverRoomOperation = ServerRoomOperation.Ready,
                roomCode = _roomCode
            });
        }

        private void OnCancelButtonClicked()
        {
            if (!_owner || _roomCode == Guid.Empty)
                return;

            NetworkClient.Send(new ServerRoomMessage
            {
                serverRoomOperation = ServerRoomOperation.Cancel
            });
        }

        private void OnLeaveButtonClicked()
        {
            if (_owner || _roomCode == Guid.Empty)
            {
                Debug.LogWarning("Not in a room");
                return;
            }

            NetworkClient.Send(new ServerRoomMessage
            {
                serverRoomOperation = ServerRoomOperation.Leave
            });
        }

        private void PlayPlayerCountChangeSound(int count)
        {
            if (_previousPlayerCount >= 0)    
            {
                if (count > _previousPlayerCount)
                    AudioManager.Instance.PlaySoundEffect(_playerJoinedSound);

                else if (count < _previousPlayerCount)
                    AudioManager.Instance.PlaySoundEffect(_playerLeftSound);
            }

            _previousPlayerCount = count;
        }

        #endregion

        #region Client Methods

        [ClientCallback]
        public void RefreshRoomPlayers(PlayerRoomInfo[] playerInfo, int maxPlayers)
        {
            while (_slots.Count < maxPlayers)
                _slots.Add(Instantiate(_playerPrefab, _playerList.transform, false));

            var count = Mathf.Min(playerInfo.Length, maxPlayers);
            var everyoneReady = true;

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var active = i < maxPlayers;
                slot.gameObject.SetActive(active);
                if (!active) continue;

                if (i < count)
                {
                    slot.SetPlayerInfo(playerInfo[i]);
                    everyoneReady &= playerInfo[i].isReady;
                }
                else
                {
                    slot.SetWaiting();
                }
            }

            _startButton.interactable = everyoneReady && _owner && count == maxPlayers;
            _playerCountText.SetText("{0} / {1}", count, maxPlayers);

            PlayPlayerCountChangeSound(count);
        }

        [ClientCallback]
        public void SetRoomCode(Guid roomCode)
        {
            _roomCode = roomCode;
            var code = roomCode.ToString("N")[..6].ToUpper();
            _roomCodeText.SetText($"Room: {code}");
        }

        [ClientCallback]
        public void SetOwner(bool owner)
        {
            _owner = owner;
            _cancelGameObject.SetActive(owner);
            _leaveGameObject.SetActive(!owner);
            _startButton.gameObject.SetActive(owner);
        }

        #endregion
    }
}
