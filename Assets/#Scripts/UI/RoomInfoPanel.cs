using Mirror;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class RoomInfoPanel : MonoBehaviour
    {
        #region Constants

        private readonly string _toggleOn = "Hit by a +2 or +4? Play another card\nto pass the total on the next player.";
        private readonly string _toggleOff = "Draw cards do not stack.\nThe next player draws and loses the turn.";

        #endregion

        #region Script References

        [Space(5f)]

        [SerializeField] private UIManager _uiManager;
        [SerializeField] private GameRulesSO _gameRules;
        [SerializeField] private ToggleData _stackCardsToggleData;

        #endregion

        #region UI References

        [Header("UI References")]
        [SerializeField] private TMP_Dropdown _maxPlayers;
        [SerializeField] private TMP_Dropdown _startingCards;

        [Space(2.5f)]

        [SerializeField] private Button _backButton;
        [SerializeField] private Button _createButton;

        [Space(2.5f)]
        [SerializeField] private TextMeshProUGUI _stackCardsInfoText;

        #endregion

        #region Sprites

        [Header("Sprites")]
        [Space(5f)]
        [SerializeField] private Sprite _onSprite;
        [SerializeField] private Sprite _offSprite;

        #endregion

        #region Runtime State

        private ToggleAnimator _cardStackAnimator;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _cardStackAnimator = new(this, _stackCardsToggleData.toggle, _onSprite, _offSprite);

            PopulateDropdown(_maxPlayers, _gameRules.MinPlayers, _gameRules.MaxPlayers, _gameRules.DefaultPlayers);
            PopulateDropdown(_startingCards, _gameRules.MinCards, _gameRules.MaxCards, _gameRules.DefaultStartingCards);

            _backButton.AddListenerSafe(OnBackButtonClicked);
            _createButton.AddListenerSafe(OnCreateButtonClicked);

            _stackCardsToggleData.toggle.AddListenerSafe(OnStackCardsToggleChanged);

            if (_uiManager is { })
                _uiManager.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            _backButton.RemoveListenerSafe(OnBackButtonClicked);
            _createButton.RemoveListenerSafe(OnCreateButtonClicked);

            _stackCardsToggleData.toggle.RemoveListenerSafe(OnStackCardsToggleChanged);

            if (_uiManager is { })
                _uiManager.OnStateChanged -= OnStateChanged;
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
            _uiManager.SetState(ScreenType.Lobby, true);
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
                allowStacking = _stackCardsToggleData.toggle.isOn
            });
        }

        private void OnStackCardsToggleChanged(bool isOn)
        {
            if (_stackCardsInfoText is not { })
                return;

            _stackCardsInfoText.text = _stackCardsToggleData.toggle.isOn ? _toggleOn : _toggleOff;

            _cardStackAnimator.Play();
        }

        private void OnStateChanged(ScreenType state)
        {
            if (state is not ScreenType.RoomInfo)
                return;

            PopulateDropdown(_maxPlayers, _gameRules.MinPlayers, _gameRules.MaxPlayers, _gameRules.DefaultPlayers);
            PopulateDropdown(_startingCards, _gameRules.MinCards, _gameRules.MaxCards, _gameRules.DefaultStartingCards);

            _stackCardsToggleData.toggle.SetIsOnWithoutNotify(true);

            _cardStackAnimator.SetVisualInstant();

            if (_stackCardsInfoText is { })
                _stackCardsInfoText.text = _toggleOn;
        }

        #endregion
    }
}