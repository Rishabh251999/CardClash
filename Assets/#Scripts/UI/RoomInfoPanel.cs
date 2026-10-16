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
        private const int MinPlayers = 3;
        private const int MaxPlayers = 8;
        private const int DefaultPlayers = 4;

        private const int MinStartingCards = 3;
        private const int MaxStartingCards = 7;
        private const int DefaultStartingCards = 4;

        #endregion

        #region Script References

        [Space(5f)]

        [SerializeField] private UIManager _uiManager;

        #endregion

        #region UI References

        [Header("UI References")]
        [SerializeField] private TMP_Dropdown _maxPlayers;
        [SerializeField] private TMP_Dropdown _startingCards;

        [Space(2.5f)]

        [SerializeField] private Button _backButton;
        [SerializeField] private Button _createButton;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            PopulateDropdown(_maxPlayers, MinPlayers, MaxPlayers, DefaultPlayers);
            PopulateDropdown(_startingCards, MinStartingCards, MaxStartingCards, DefaultStartingCards);

            if(_backButton is { })
                _backButton.onClick.AddListener(OnBackButtonClicked);

            if(_createButton is { })
                _createButton.onClick.AddListener(OnCreateButtonClicked);

            if (_uiManager is { })
                _uiManager.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (_backButton is { })
                _backButton.onClick.RemoveListener(OnBackButtonClicked);

            if (_createButton is { })
                _createButton.onClick.RemoveListener(OnCreateButtonClicked);

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

        private void OnBackButtonClicked() => _uiManager.SetState(ScreenType.Lobby, true);

        private void OnCreateButtonClicked()
        {
            var maxPlayers = _maxPlayers.value + MinPlayers;
            var startingCards = _startingCards.value + MinStartingCards;

            NetworkClient.Send(new ServerRoomMessage
            {
                serverRoomOperation = ServerRoomOperation.Create,
                maxPlayers = maxPlayers,
                startingCards = startingCards
            });
        }

        private void OnStateChanged(ScreenType state)
        {
            if (state is not ScreenType.RoomInfo)
                return;

            PopulateDropdown(_maxPlayers, MinPlayers, MaxPlayers, DefaultPlayers);
            PopulateDropdown(_startingCards, MinStartingCards, MaxStartingCards, DefaultStartingCards);
        }

        #endregion
    }
}