using Mirror;
using System.Collections;
using UnityEngine;

namespace CardClash
{
    /// <summary>
    /// Client-side turn/game countdown UI (timer ring, text), the local
    /// wild-color auto-pick timeout, and the pre-game start countdown sequence.
    /// </summary>
    internal sealed class GameTimerController
    {
        private readonly CardGameController _controller;

        private bool _isGameRunningLocally;
        private bool _isTimerRunningLocally;
        private int _lastDisplayedGameSecond = -1;

        private Coroutine _countdownCoroutine;

        internal GameTimerController(CardGameController controller)
        {
            _controller = controller;
        }

        internal void Tick()
        {
            UpdateTurnTimerUI();
            UpdateGameTimerUI();
            CheckPendingWildColorTimeout();
        }

        private void UpdateTurnTimerUI()
        {
            var turnTimer = new NetworkTimer(_controller.TurnStartTime, _controller.GameRules.TurnTimeLimit);
            var normalized = turnTimer.Normalized;

            var isLocalPlayersTurn = _controller.CurrentPlayerNetId == NetworkClient.localPlayer.netId;

            if (_controller.TurnTimerRingImage != null && isLocalPlayersTurn)
            {
                _controller.TurnTimerRingImage.fillAmount = normalized;

                if (turnTimer.HasExpired)
                    _isTimerRunningLocally = false;
            }

            if (_controller.PlayerManager.GetPlayerGUI(_controller.CurrentPlayerNetId) is { } currentPlayerGUI)
                currentPlayerGUI.UpdateTurnTimer(normalized);
        }

        private void UpdateGameTimerUI()
        {
            if (!_isGameRunningLocally || _controller.GameTimerText == null)
                return;

            var gameTimer = new NetworkTimer(_controller.GameStartTime, _controller.GameRules.GameTimeLimit);
            var totalSeconds = Mathf.FloorToInt(gameTimer.RemainingTime);

            if (totalSeconds != _lastDisplayedGameSecond)
            {
                _lastDisplayedGameSecond = totalSeconds;

                var minutes = totalSeconds / 60;
                var seconds = totalSeconds % 60;

                _controller.GameTimerText.SetText($"{minutes:00}:{seconds:00}");
            }

            if (gameTimer.HasExpired)
                _isGameRunningLocally = false;
        }

        private void CheckPendingWildColorTimeout()
        {
            if (!_controller.UIController.HasPendingColorChoice) return;

            if (NetworkClient.localPlayer is not { netId: var selfNetId } || _controller.CurrentPlayerNetId != selfNetId)
                return;

            var turnTimer = new NetworkTimer(_controller.TurnStartTime, _controller.GameRules.TurnTimeLimit);

            if (turnTimer.RemainingTime > 0.3f)
                return;

            _controller.UIController.HandleColorPicked((CardColor)UnityEngine.Random.Range(1, 5)); // Red..Blue
        }

        internal void HandleTurnStartTimeChanged(double oldValue, double newValue)
        {
            var isMyTurn = NetworkClient.localPlayer is { netId: var selfNetId } && _controller.CurrentPlayerNetId == selfNetId;

            _isTimerRunningLocally = isMyTurn;

            if (_controller.TurnTimerRingImage != null)
            {
                _controller.TurnTimerRingImage.fillAmount = 1f;
            }
        }

        internal void HandleGameStartTimeChanged(double oldValue, double newValue)
        {
            _isGameRunningLocally = true;

            var timer = $"{Mathf.FloorToInt(_controller.GameRules.GameTimeLimit / 60f):00}:" +
                    $"{Mathf.FloorToInt(_controller.GameRules.GameTimeLimit % 60f):00}";

            _controller.GameTimerText.SetText($"{timer}");
        }

        #region Countdown

        internal void PlayStartCountdown()
        {
            if (_countdownCoroutine != null)
            {
                _controller.StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            _countdownCoroutine = _controller.StartCoroutine(StartCountdownRoutine());
        }

        private IEnumerator StartCountdownRoutine()
        {
            _controller.UIController.SetPanelVisible(_controller.CountdownPanel, true);

            (string Text, string Sfx)[] steps =
            {
                ("3", "countdown_beep"),
                ("2", "countdown_beep"),
                ("1", "countdown_beep"),
                ("GO!", "countdown_go")
            };

            foreach (var step in steps)
            {
                yield return _controller.StartCoroutine(PlayCountdownStep(step.Text, step.Sfx));
            }

            _controller.UIController.SetPanelVisible(_controller.CountdownPanel, false);

            _countdownCoroutine = null;
        }

        private IEnumerator PlayCountdownStep(string text, string sfxName)
        {
            if (_controller.CountdownText == null)
                yield break;

            const float startScale = 1.6f;
            const float endScale = 1f;

            _controller.CountdownText.text = text;

            AudioManager.Instance.PlaySoundEffect(sfxName);

            var rect = _controller.CountdownText.rectTransform;

            rect.localScale = Vector3.one * startScale;

            var elapsed = 0f;

            while (elapsed < _controller.CountdownStepDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / _controller.CountdownStepDuration);

                float scale = Mathf.Lerp(startScale, endScale, 1f - (1f - t) * (1f - t));

                rect.localScale = Vector3.one * scale;

                yield return null;
            }

            rect.localScale = Vector3.one * endScale;
        }

        internal void StopCountdownIfRunning()
        {
            if (_countdownCoroutine != null)
            {
                _controller.StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }
        }

        #endregion
    }
}
