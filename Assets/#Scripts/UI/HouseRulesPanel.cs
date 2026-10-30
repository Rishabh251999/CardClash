using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CardClash
{
    public class HouseRulesPanel : MonoBehaviour
    {
        #region Constants/Readonly

        private const int DefaultCardsPerTurn = 1;
        private const int MinCardsPerTurn = 2;
        private const int MaxCardsPerTurn = 4;

        #endregion

        #region Script References

        [SerializeField] private UIManager _uiManager;

        #endregion

        #region UI References

        [Header("UI References")]
        [Space(5f)]
        [SerializeField] private Toggle _cardStackToggle;
        [SerializeField] private Toggle _lastCardPowerCardToggle;
        [SerializeField] private Toggle _playSameNumberToggle;

        [Space(2.5f)]

        [SerializeField] private TMP_Dropdown _maxCardsPerTurnDropdown;

        [Space(2.5f)]

        [SerializeField] private CanvasGroup _maxCardsPerTurnGroup;

        [Space(2.5f)]

        [SerializeField] private Button _backButton;

        #endregion

        #region Sprites References

        [Header("Sprites References")]
        [Space(5f)]
        [SerializeField] private Sprite _onSprite;
        [SerializeField] private Sprite _offSprite;

        #endregion

        #region Runtime State

        private UnityAction<bool> _cardStackCallback;
        private UnityAction<bool> _lastCardPowerCardCallback;
        private UnityAction<bool> _playSameNumberCallback;

        public event UnityAction OnRulesChanged;

        private ToggleAnimator _cardStackToggleAnimator;
        private ToggleAnimator _lastCardPowerCardToggleAnimator;
        private ToggleAnimator _playSameNumberToggleAnimator;

        internal bool CardStackEnabled => _cardStackToggle.isOn;
        internal bool LastCardPowerCardEnabled => _lastCardPowerCardToggle.isOn;
        internal bool PlaySameNumberEnabled => _playSameNumberToggle.isOn;

        internal int MaxCardsPerTurnValue => DefaultCardsPerTurn + _maxCardsPerTurnDropdown.value;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        { 
            InitializeToggleAnimators();

            InitializeCallbacks();

            PopulateMaxCardsPerTurnDropdown();

            UpdateMaxCardsPerTurnInteractable(_playSameNumberToggle.isOn);
        }

        private void Start()
        {
            RegisterListeners();
        }

        private void OnDestroy()
        {
            UnregisterListeners();
        }

        private void InitializeToggleAnimators()
        {
            _cardStackToggleAnimator = new(this, _cardStackToggle, _onSprite, _offSprite);
            _lastCardPowerCardToggleAnimator = new(this, _lastCardPowerCardToggle, _onSprite, _offSprite);
            _playSameNumberToggleAnimator = new(this, _playSameNumberToggle, _onSprite, _offSprite);
        }

        private void InitializeCallbacks()
        {
            _cardStackCallback = _ =>
            {
                _cardStackToggleAnimator.Play();
                OnRulesChanged?.Invoke();
            };

            _lastCardPowerCardCallback = _ =>
            {
                _lastCardPowerCardToggleAnimator.Play();
                OnRulesChanged?.Invoke();
            };

            _playSameNumberCallback = isEnabled =>
            {
                _playSameNumberToggleAnimator.Play();
                UpdateMaxCardsPerTurnInteractable(isEnabled);

                OnRulesChanged?.Invoke();
            };
        }

        private void RegisterListeners()
        {
            _backButton.AddListenerSafe(OnBackButtonClicked);

            _cardStackToggle.AddListenerSafe(_cardStackCallback);
            _lastCardPowerCardToggle.AddListenerSafe(_lastCardPowerCardCallback);
            _playSameNumberToggle.AddListenerSafe(_playSameNumberCallback);
        }

        private void UnregisterListeners()
        {
            _backButton.RemoveListenerSafe(OnBackButtonClicked);

            _cardStackToggle.RemoveListenerSafe(_cardStackCallback);
            _lastCardPowerCardToggle.RemoveListenerSafe(_lastCardPowerCardCallback);
            _playSameNumberToggle.RemoveListenerSafe(_playSameNumberCallback);
        }

        private void PopulateMaxCardsPerTurnDropdown()
        {
            _maxCardsPerTurnDropdown.ClearOptions();

            var options = new List<TMP_Dropdown.OptionData>();
            for (var i = MinCardsPerTurn; i <= MaxCardsPerTurn; i++)
                options.Add(new(i.ToString()));

            _maxCardsPerTurnDropdown.AddOptions(options);
            _maxCardsPerTurnDropdown.SetValueWithoutNotify(0); // defaults to MinCardsPerTurn (2)
        }

        private void UpdateMaxCardsPerTurnInteractable(bool isEnabled)
        {
            _maxCardsPerTurnGroup.interactable = isEnabled;
            _maxCardsPerTurnGroup.blocksRaycasts = isEnabled;
            _maxCardsPerTurnGroup.alpha = isEnabled ? 1f : 0.5f;
        }

        private void OnBackButtonClicked()
        {
            _uiManager.SetState(ScreenType.RoomInfo, false);
        }


        internal void ResetToDefaults()
        {
            ResetToggle(_cardStackToggle, _cardStackToggleAnimator, true);
            ResetToggle(_lastCardPowerCardToggle, _lastCardPowerCardToggleAnimator, true);
            ResetToggle(_playSameNumberToggle, _playSameNumberToggleAnimator, true);

            _maxCardsPerTurnDropdown.SetValueWithoutNotify(0);
            UpdateMaxCardsPerTurnInteractable(true);

            OnRulesChanged?.Invoke();
        }

        private void ResetToggle(Toggle toggle, ToggleAnimator animator, bool defaultValue)
        {
            toggle.SetIsOnWithoutNotify(defaultValue);
            animator.SetVisualInstant();
        }

        #endregion
    }
}
