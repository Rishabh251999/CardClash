using Mirror;
using System.Collections;
using UnityEngine;

namespace CardClash
{
    /// <summary>
    /// Notification display/hide timing, discard/skip/reverse visual effects,
    /// and end-game/penalty sound effects. RPC/TargetRpc methods themselves must
    /// stay physically declared on <see cref="CardGameController"/> (Mirror
    /// weaving constraint); this class holds their visual/audio bodies.
    /// </summary>
    internal sealed class GameEffectsController
    {
        private readonly CardGameController _controller;

        private Coroutine _notificationHideCoroutine;

        internal GameEffectsController(CardGameController controller)
        {
            _controller = controller;
        }

        #region Notification

        internal void ResetNotificationHideTimer()
        {
            if (_notificationHideCoroutine is { })
            {
                _controller.StopCoroutine(_notificationHideCoroutine);
                _notificationHideCoroutine = null;
            }

            _notificationHideCoroutine = _controller.StartCoroutine(
                HideNotificationAfterDelay(_controller.GameRules.NotificationDisplayDuration));
        }

        private IEnumerator HideNotificationAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            _notificationHideCoroutine = null;
            _controller.RpcHideNotification();
        }

        internal void StopNotificationTimer()
        {
            if (_notificationHideCoroutine is { })
                _controller.StopCoroutine(_notificationHideCoroutine);
        }

        internal void ShowNotificationVisual(NotificationType notificationType, string text) =>
            _controller.NotificationView.Show(notificationType, text);

        internal void HideNotificationVisual() => _controller.NotificationView.Hide();

        #endregion


        #region Card Visuals

        internal void ShowTopDiscardVisual(GameCard card, int drawCount)
        {
            if (_controller.CardVisualDatabase.GetSprite(card) is { } sprite)
            {
                _controller.TopDiscardImage.color = new Color(1f, 1f, 1f, 1f);
                _controller.TopDiscardImage.sprite = sprite;
            }

            _controller.DrawPileCountText.SetText("{0} left", drawCount);

            _controller.ReverseFlipbook.StartRotating(_controller.ToUnityColor(card.Color));
        }

        internal void PlayDiscardEffectVisual(GameCard card, int turnDirection)
        {
            if (card.Type is CardType.Reverse && _controller.ReverseFlipbook is { })
            {
                _controller.ReverseFlipbook.SetDirection(turnDirection);

                AudioManager.Instance.PlaySoundEffect("reverse");
                VibrationUtility.Vibrate(milliseconds: 250, amplitude: 255);
            }
        }

        internal void PlaySkipEffectVisual(uint skippedNetId, Color tint)
        {
            if (_controller.PlayerManager.GetPlayerGUI(skippedNetId) is { } skippedPlayerGUI)
                skippedPlayerGUI.PlaySkipEffect(tint);

            // Only play the big on-screen skip flipbook, sound, and vibration
            // for the player who was actually skipped.
            if (NetworkClient.localPlayer is { netId: var selfNetId } && selfNetId == skippedNetId)
            {
                if (_controller.SkipFlipbook is { })
                {
                    _controller.SkipFlipbook.Play(tint);
                }

                AudioManager.Instance.PlaySoundEffect("skip");
                VibrationUtility.Vibrate(milliseconds: 250, amplitude: 255);
            }
        }

        #endregion


        #region Sound Effects

        internal void PlayGameEndSfx(string soundName)
        {
            if (string.IsNullOrEmpty(soundName) || AudioManager.Instance == null)
                return;

            AudioManager.Instance.PlaySoundEffect(soundName);
        }

        internal void PlayPenaltyDrawSound()
        {
            AudioManager.Instance.PlaySoundEffect("draw_penalty");
        }

        #endregion
    }
}
