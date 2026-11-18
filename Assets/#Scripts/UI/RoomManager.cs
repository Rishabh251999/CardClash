using Mirror;
using System;
using System.Collections;
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

        [SerializeField] private HouseRules _houseRules;

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

        private CanvasGroup _houseRulesCanvasGroup;

        #endregion

        #region GameObject References

        [Space(5)]
        [Header("GameObject References")]

        [SerializeField] private GameObject _playerList;
        [SerializeField] private GameObject _leaveGameObject;
        [SerializeField] private GameObject _cancelGameObject;
        [SerializeField] private GameObject _houseRulesGameObject;
        [SerializeField] private GameObject _startButtonGameObject;

        #endregion

        #region Runtime State

        private bool _owner;

        private Guid _roomCode = Guid.Empty;

        private int _previousPlayerCount = -1;

        private readonly List<PlayerGUI> _slots = new();

        private Coroutine _houseRulesAnimation;

        private ToggleAnimator _stackDrawCardsAnimator;
        private ToggleAnimator _noPowerCardFinishAnimator;
        private ToggleAnimator _playSameNumberAnimator;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _startButton = _startButtonGameObject.GetComponent<Button>();
            _cancelButton = _cancelGameObject.GetComponent<Button>();
            _leaveButton = _leaveGameObject.GetComponent<Button>();

            _houseRulesCanvasGroup = _houseRulesGameObject.GetComponent<CanvasGroup>();

            _stackDrawCardsAnimator = new(this, _houseRules._stackDrawCardsToggle, _houseRules._onSprite, _houseRules._offSprite);
            _noPowerCardFinishAnimator = new(this, _houseRules._noPowerCardFinishToggle, _houseRules._onSprite, _houseRules._offSprite);
            _playSameNumberAnimator = new(this, _houseRules._playSameNumberToggle, _houseRules._onSprite, _houseRules._offSprite);
        }

        private void Start()
        {
            _leaveButton.AddListenerSafe(OnLeaveButtonClicked);
            _startButton.AddListenerSafe(OnStartButtonClicked);
            _readyButton.AddListenerSafe(OnReadyButtonClicked);
            _cancelButton.AddListenerSafe(OnCancelButtonClicked);
        }

        private void OnDestroy()
        {
            _startButton.RemoveListenerSafe(OnStartButtonClicked);
            _readyButton.RemoveListenerSafe(OnReadyButtonClicked);
            _leaveButton.RemoveListenerSafe(OnLeaveButtonClicked);
            _cancelButton.RemoveListenerSafe(OnCancelButtonClicked);
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

        public void SetHouseRulesPanelVisible(bool visible)
        {
            if (_houseRulesGameObject.transform is not RectTransform rect)
                return;

            if (_houseRulesAnimation != null)
                StopCoroutine(_houseRulesAnimation);

            if (visible)
                _houseRulesCanvasGroup.blocksRaycasts = true;
            else
                _houseRulesCanvasGroup.interactable = false;

            _houseRulesAnimation = StartCoroutine(visible ? PopInPanel(rect) : PopOutPanel(rect));
        }

        private IEnumerator PopInPanel(RectTransform rect)
        {
            const float overshootScale = 1.05f;
            const float scaleUpDuration = 0.2f;
            const float scaleDownDuration = 0.2f;

            rect.localScale = Vector3.zero;
            _houseRulesCanvasGroup.alpha = 0f;

            var elapsed = 0f;
            while (elapsed < scaleUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / scaleUpDuration);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(0f, overshootScale, t);
                _houseRulesCanvasGroup.alpha = t;
                yield return null;
            }

            rect.localScale = Vector3.one * overshootScale;
            elapsed = 0f;
            while (elapsed < scaleDownDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / scaleDownDuration);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(overshootScale, 1f, t);
                yield return null;
            }

            rect.localScale = Vector3.one;
            _houseRulesCanvasGroup.interactable = true;
        }

        private IEnumerator PopOutPanel(RectTransform rect)
        {
            const float scaleDownDuration = 0.2f;
            var startScale = rect.localScale;
            var startAlpha = _houseRulesCanvasGroup.alpha;
            var elapsed = 0f;

            while (elapsed < scaleDownDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / scaleDownDuration);
                rect.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, t);
                _houseRulesCanvasGroup.alpha = Mathf.LerpUnclamped(startAlpha, 0f, t);
                yield return null;
            }

            rect.localScale = Vector3.zero;
            _houseRulesCanvasGroup.blocksRaycasts = false;
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

        private void UpdateMaxCardsPerTurnInteractable(bool isEnabled)
        {
            _houseRules._maxCardsPerTurnGroup.interactable = isEnabled;
            _houseRules._maxCardsPerTurnGroup.blocksRaycasts = isEnabled;
            _houseRules._maxCardsPerTurnGroup.alpha = isEnabled ? 1f : 0.5f;
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
            _playerCountText.SetText("{0} / {1}", (float)count, (float)maxPlayers);


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

        [ClientCallback]
        public void SetHouseRules(bool allowStacking, bool noPowerCardFinish, bool playSameNumber, int maxCardsPerTurn)
        {
            _houseRules._stackDrawCardsToggle.SetIsOnWithoutNotify(allowStacking);
            _stackDrawCardsAnimator.SetVisualInstant();

            _houseRules._noPowerCardFinishToggle.SetIsOnWithoutNotify(noPowerCardFinish);
            _noPowerCardFinishAnimator.SetVisualInstant();

            _houseRules._playSameNumberToggle.SetIsOnWithoutNotify(playSameNumber);
            _playSameNumberAnimator.SetVisualInstant();

            _houseRules._maxCardsPerTurnText.SetText("{0}", (float)maxCardsPerTurn);

            UpdateMaxCardsPerTurnInteractable(playSameNumber);
        }

        #endregion
    }

    [Serializable]
    public struct HouseRules
    {
        public Toggle _stackDrawCardsToggle;
        public Toggle _noPowerCardFinishToggle;
        public Toggle _playSameNumberToggle;
        public TextMeshProUGUI _maxCardsPerTurnText;

        public CanvasGroup _maxCardsPerTurnGroup;

        public Sprite _onSprite;
        public Sprite _offSprite;
    }
}
