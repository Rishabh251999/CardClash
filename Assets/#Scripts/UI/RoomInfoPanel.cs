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

        #region UI References

        [Header("UI References")]
        [SerializeField] private TMP_Dropdown _maxPlayers;
        [SerializeField] private TMP_Dropdown _startingCards;

        [Space(2.5f)]

        [SerializeField] private Button _createButton;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            PopulateDropdown(_maxPlayers, MinPlayers, MaxPlayers, DefaultPlayers);
            PopulateDropdown(_startingCards, MinStartingCards, MaxStartingCards, DefaultStartingCards);
        }

        private void Start()
        {
            _createButton.onClick.AddListener(OnCreateButtonClicked);
        }

        private void PopulateDropdown(TMP_Dropdown dropdown, int min, int max, int defaultValue)
        {
            if (dropdown == null)
            {
                return;
            }

            List<string> options = new();
            int defaultIndex = 0;

            for (int value = min; value <= max; value++)
            {
                options.Add(value.ToString());

                if (value == defaultValue)
                {
                    defaultIndex = options.Count - 1;
                }
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.value = defaultIndex;
            dropdown.RefreshShownValue();
        }

        private void OnCreateButtonClicked()
        {
            var maxPlayers = int.Parse(_maxPlayers.options[_maxPlayers.value].text);
            var startingCards = int.Parse(_startingCards.options[_startingCards.value].text);

            Debug.Log($"Creating room with max players: {maxPlayers}, starting cards: {startingCards}");

            NetworkClient.Send(new ServerRoomMessage
            {
                serverRoomOperation = ServerRoomOperation.Create,
                maxPlayers = maxPlayers,
                startingCards = startingCards
            });
        }

        #endregion
    }
}