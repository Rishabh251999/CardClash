using DG.Tweening;
using Mirror;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace CardClash
{
    public class RoomInfoPanel : MonoBehaviour
    {
        #region Constants

        private enum HouseRuleType
        {
            CardStacking,
            NoPowerCardFinish,
            PlaySameNumber,
        }

        private readonly Dictionary<HouseRuleType, GameObject> _spawnedRules = new();

        #endregion

        #region Script References

        [SerializeField] private UIManager _uiManager;
        [SerializeField] private GameRulesSO _gameRules;
        [SerializeField] private HouseRulesPanel _houseRulesPanel;

        #endregion

        #region UI References

        [Header("UI References")]
        [Space(5f)]
        [SerializeField] private TMP_Dropdown _maxPlayers;
        [SerializeField] private TMP_Dropdown _startingCards;

        [Space(2.5f)]

        [SerializeField] private TextMeshProUGUI _houseRulesStatusText;

        [Space(2.5f)]

        [SerializeField] private Button _backButton;
        [SerializeField] private Button _createButton;
        [SerializeField] private Button _houseRulesButton;

        #endregion

        #region Runtime References

        [Space(5f)]
        [SerializeField] private GameObject _ruleGameobject;
        [Space(2.5f)]
        [SerializeField] private Transform _ruleParentTransform;

        #endregion

        #region Runtime State

        private ScreenType _previousState;

        private Transform _transform;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeRules();
        }

        private void Start()
        {
            _transform = _houseRulesButton.transform.GetChild(2);

            PopulateDropdown(_maxPlayers, _gameRules.MinPlayers, _gameRules.MaxPlayers, _gameRules.DefaultPlayers);
            PopulateDropdown(_startingCards, _gameRules.MinCards, _gameRules.MaxCards, _gameRules.DefaultStartingCards);

            _backButton.AddListenerSafe(OnBackButtonClicked);
            _createButton.AddListenerSafe(OnCreateButtonClicked);
            _houseRulesButton.AddListenerSafe(OnHouseRulesButtonClicked);

            _houseRulesPanel.OnRulesChanged += UpdateRulesVisibility;

            if (_uiManager is { })
                _uiManager.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            _backButton.RemoveListenerSafe(OnBackButtonClicked);
            _createButton.RemoveListenerSafe(OnCreateButtonClicked);
            _houseRulesButton.RemoveListenerSafe(OnHouseRulesButtonClicked);

            _houseRulesPanel.OnRulesChanged -= UpdateRulesVisibility;

            if (_uiManager is { })
                _uiManager.OnStateChanged -= OnStateChanged;
        }

        private void InitializeRules()
        {
            if (_ruleGameobject == null || _ruleParentTransform == null)
                return;

            foreach (HouseRuleType ruleType in System.Enum.GetValues(typeof(HouseRuleType)))
            {
                var rule = Instantiate(_ruleGameobject, _ruleParentTransform);

                _spawnedRules.Add(ruleType, rule);
            }

            UpdateRulesVisibility();
        }

        private void PopulateDropdown(TMP_Dropdown dropdown, int min, int max, int defaultValue)
        {
            if (dropdown is not { })
                return;

            var options = new List<string>(max - min + 1);
            var defaultIndex = Mathf.Clamp(defaultValue, min, max) - min;

            for (var value = min; value <= max; value++)
                options.Add($"{value}");

            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.value = defaultIndex;
            dropdown.RefreshShownValue();
        }

        private void OnBackButtonClicked()
        {
            _uiManager.SetState(ScreenType.Lobby, false);
        }

        private void OnCreateButtonClicked()
        {
            var maxPlayers = _maxPlayers.value + _gameRules.MinPlayers;
            var startingCards = _startingCards.value + _gameRules.MinCards;

            NetworkClient.Send(new ServerRoomMessage
            {
                serverRoomOperation = ServerRoomOperation.Create,
                maxPlayers = maxPlayers,
                startingCards = startingCards,
                allowStacking = _houseRulesPanel.CardStackEnabled,
                noPowerCardFinish = _houseRulesPanel.LastCardPowerCardEnabled,
                playSameNumber = _houseRulesPanel.PlaySameNumberEnabled,   
                maxCardsPerTurn = _houseRulesPanel.MaxCardsPerTurnValue
            });
        }

        private void OnHouseRulesButtonClicked()
        {
            _transform.DORotate(new Vector3(_transform.eulerAngles.x, _transform.eulerAngles.y, 
                _transform.eulerAngles.z - 90f), 0.25f).SetEase(Ease.InOutSine);

            _uiManager.SetState(ScreenType.HouseRules, false);
        }

        private void UpdateRulesVisibility()
        {
            if (_ruleParentTransform == null || _houseRulesPanel == null)
                return;

            var activeRules = 0;

            foreach (HouseRuleType ruleType in Enum.GetValues(typeof(HouseRuleType)))
            {
                var isEnabled = ruleType switch
                {
                    HouseRuleType.CardStacking => _houseRulesPanel.CardStackEnabled,

                    HouseRuleType.NoPowerCardFinish => _houseRulesPanel.LastCardPowerCardEnabled,

                    HouseRuleType.PlaySameNumber => _houseRulesPanel.PlaySameNumberEnabled,

                    _ => false
                };

                if (!_spawnedRules.TryGetValue(ruleType, out var rule))
                    continue;

                rule.SetActive(isEnabled);

                if (!isEnabled)
                    continue;

                activeRules++;

                var text = rule.GetComponentInChildren<TextMeshProUGUI>();

                if (text != null)
                    text.text = GetRuleName(ruleType);
            }

            _ruleParentTransform.gameObject.SetActive(activeRules > 0);
        }

        private string GetRuleName(HouseRuleType ruleType)
        {
            return ruleType switch
            {
                HouseRuleType.CardStacking => "Stacking",
                HouseRuleType.NoPowerCardFinish => "No Power Finish",
                HouseRuleType.PlaySameNumber => "Multi-play",
                _ => string.Empty
            };
        }

        private void OnStateChanged(ScreenType state)
        {
            if (state is ScreenType.RoomInfo && _previousState != ScreenType.HouseRules)
            {
                _houseRulesPanel.ResetToDefaults();
            }

            if (state is not ScreenType.RoomInfo)
            {
                _previousState = state;
                return;
            }

            if (_transform.eulerAngles.z != 0)
                _transform.DORotate(new Vector3(_transform.eulerAngles.x, _transform.eulerAngles.y, 0f), 0.25f)
                    .SetEase(Ease.InOutSine);

            PopulateDropdown(_maxPlayers, _gameRules.MinPlayers, _gameRules.MaxPlayers, _gameRules.DefaultPlayers);
            PopulateDropdown(_startingCards, _gameRules.MinCards, _gameRules.MaxCards, _gameRules.DefaultStartingCards);

            UpdateRulesVisibility();

            _previousState = state;
        }

        #endregion
    }
}