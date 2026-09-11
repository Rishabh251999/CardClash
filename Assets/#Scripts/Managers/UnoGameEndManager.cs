using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UNO
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UnoGameEndManager : MonoBehaviour
    {
        #region UI References

        [SerializeField] private GameEndPlayerGUI _playerGUI;

        [Space(5)]

        [SerializeField] private TextMeshProUGUI _winnerText;
        [SerializeField] private Button _backToLobbyButton;
        private CanvasGroup _canvasGroup;

        [Space(5)]

        [SerializeField] private RectTransform _standingsListContainer;

        #endregion

        #region Runtime Collections

        private readonly List<GameEndPlayerGUI> _spawnedRows = new();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }


        private void Start()
        {
            _backToLobbyButton.onClick.AddListener(OnClickBackToLobby);
        }


        private void OnDestroy()
        {
            _backToLobbyButton.onClick.RemoveListener(OnClickBackToLobby);
        }


        private void OnClickBackToLobby()
        {
            HideGameEndScreen();

            UnoNetworkManager.Singleton._gameManager.OnRoomLeft();
        }


        public void ShowGameEndScreen(IReadOnlyList<GameEndPlayerInfo> standings)
        {
            SetVisible(true);

            var sortedStandings = standings.OrderBy(p => p.PlayerScore).ToList();

            if (sortedStandings.Count > 0)
            {
                _winnerText.SetText($"{sortedStandings[0].PlayerName} Wins!");
            }

            foreach(var i in sortedStandings)
            {
                var playerGUI = Instantiate(_playerGUI, _standingsListContainer);
                playerGUI.SetPlayerInfo(i.PlayerName, i.PlayerScore);

                _spawnedRows.Add(playerGUI);
            }
        }


        public void HideGameEndScreen()
        {
            SetVisible(false);
            ClearStandingsRows();
        }


        private void ClearStandingsRows()
        {
            foreach (var row in _spawnedRows)
                if (row != null)
                    Destroy(row.gameObject);

            _spawnedRows.Clear();
        }


        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        #endregion
    }
}