using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class CardGameEndManager : MonoBehaviour
    {
        #region UI References

        [SerializeField] private GameEndPlayerGUI _playerGUI;

        [Space(5)]

        [SerializeField] private TextMeshProUGUI _winnerText;
        [Space(2.5f)]
        [SerializeField] private Button _playAgainButton;
        [SerializeField] private Button _backToLobbyButton;
        private RectTransform _rectTransform;
        private Coroutine _visibilityCoroutine;

        [Space(5)]

        [SerializeField] private RectTransform _standingsListContainer;

        #endregion

        #region Runtime Collections

        private readonly List<GameEndPlayerGUI> _spawnedRows = new();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
            SetVisible(false);
        }


        private void Start()
        {
            _backToLobbyButton.AddListenerSafe(OnClickBackToLobby);
            _playAgainButton.AddListenerSafe(OnClickPlayAgain);
        }


        private void OnDestroy()
        {
            _backToLobbyButton.RemoveListenerSafe(OnClickBackToLobby);
            _playAgainButton.RemoveListenerSafe(OnClickPlayAgain);
        }


        private void OnClickBackToLobby()
        {
            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.QuitMatch
            });

            HideGameEndScreen();

            CardNetworkManager.Singleton._gameManager.OnRoomLeft();
        }

        private void OnClickPlayAgain()
        {
            CardGameController.Instance.CmdRequestPlayAgain();
        }


        public void ShowGameEndScreen(IReadOnlyList<GameEndPlayerInfo> standings)
        {
            SetVisible(true);

            _playAgainButton.gameObject.SetActive(false);

            var sortedStandings = standings.OrderBy(p => p.PlayerScore).ToList();

            if (sortedStandings.Count > 0)
            {
                _winnerText.SetText($"{sortedStandings[0].PlayerName} Wins!");
            }

            foreach (var i in sortedStandings)
            {
                var playerGUI = Instantiate(_playerGUI, _standingsListContainer);
                int index = _spawnedRows.Count + 1;
                playerGUI.SetPlayerInfo(index, i.PlayerName, i.PlayerScore, i.AvatarId);

                _spawnedRows.Add(playerGUI);
            }
        }


        public void HideGameEndScreen()
        {
            SetVisible(false);
            ClearStandingsRows();

            _playAgainButton.gameObject.SetActive(false);
        }

        public void ShowPlayAgainButton() => _playAgainButton.gameObject.SetActive(true);

        private void ClearStandingsRows()
        {
            foreach (var row in _spawnedRows)
                if (row != null)
                    Destroy(row.gameObject);

            _spawnedRows.Clear();
        }


        private void SetVisible(bool visible)
        {
            if (_rectTransform == null)
                return;

            if (_visibilityCoroutine != null)
                StopCoroutine(_visibilityCoroutine);

            _visibilityCoroutine = StartCoroutine(visible ? PopIn() : PopOut());
        }

        private IEnumerator PopIn()
        {
            const float overshootScale = 1.05f;
            const float scaleUpDuration = 0.12f;
            const float scaleDownDuration = 0.18f;

            _rectTransform.localScale = Vector3.zero;

            var elapsed = 0f;

            while (elapsed < scaleUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = Mathf.Clamp01(elapsed / scaleUpDuration);

                _rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0f, overshootScale, t);

                yield return null;
            }

            _rectTransform.localScale = Vector3.one * overshootScale;

            elapsed = 0f;

            while (elapsed < scaleDownDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = Mathf.Clamp01(elapsed / scaleDownDuration);

                _rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(overshootScale, 1f, t);

                yield return null;
            }

            _rectTransform.localScale = Vector3.one;
        }

        private IEnumerator PopOut()
        {
            const float scaleDownDuration = 0.12f;

            var startScale = _rectTransform.localScale;
            var elapsed = 0f;

            while (elapsed < scaleDownDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = Mathf.Clamp01(elapsed / scaleDownDuration);

                _rectTransform.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, t);

                yield return null;
            }

            _rectTransform.localScale = Vector3.zero;
        }

        #endregion
    }
}