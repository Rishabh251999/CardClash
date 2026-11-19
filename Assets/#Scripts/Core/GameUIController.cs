using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardClash
{
    /// <summary>
    /// Button wiring, pause/resume and color-picker panel visibility/animation,
    /// and the draw/pass/last-card client input handlers.
    /// </summary>
    internal sealed class GameUIController
    {
        private readonly CardGameController _controller;
        private readonly Dictionary<GameObject, Coroutine> _panelAnimations = new();

        private Action<CardColor> _onColorChosen;
        private Card _pendingWildCard;

        internal GameUIController(CardGameController controller)
        {
            _controller = controller;
        }

        internal bool HasPendingColorChoice => _onColorChosen != null;

        #region Button Wiring

        internal void WireButtons()
        {
            _controller.QuitButton.AddListenerSafe(OnClickQuitButton);
            _controller.PauseButton.AddListenerSafe(OnPauseButtonClicked);
            _controller.ResumeButton.AddListenerSafe(OnResumeButtonClicked);

            _controller.CardDrawButton.AddListenerSafe(OnDrawButtonClicked);
            _controller.PassTurnButton.AddListenerSafe(OnPassButtonClicked);
            _controller.LastCardButton.AddListenerSafe(OnLastCardButtonClicked);
            _controller.PlaySelectedButton.AddListenerSafe(_controller.HandController.OnPlaySelectedButtonClicked);

            _controller.GameButtonManager.SetPassButtonInteractable(false);

            _controller.RedColorButton.AddListenerSafe(OnRedColorClicked);
            _controller.BlueColorButton.AddListenerSafe(OnBlueColorClicked);
            _controller.GreenColorButton.AddListenerSafe(OnGreenColorClicked);
            _controller.YellowColorButton.AddListenerSafe(OnYellowColorClicked);

            SetPanelVisible(_controller.PausePanel, false);
            SetPanelVisible(_controller.CountdownPanel, false);
            SetPanelVisible(_controller.ColorPickerPanel, false);
        }

        internal void UnwireButtons()
        {
            _controller.CardDrawButton.RemoveListenerSafe(OnDrawButtonClicked);
            _controller.PassTurnButton.RemoveListenerSafe(OnPassButtonClicked);
            _controller.LastCardButton.RemoveListenerSafe(OnLastCardButtonClicked);
            _controller.PlaySelectedButton.RemoveListenerSafe(_controller.HandController.OnPlaySelectedButtonClicked);

            _controller.RedColorButton.RemoveListenerSafe(OnRedColorClicked);
            _controller.YellowColorButton.RemoveListenerSafe(OnYellowColorClicked);
            _controller.GreenColorButton.RemoveListenerSafe(OnGreenColorClicked);
            _controller.BlueColorButton.RemoveListenerSafe(OnBlueColorClicked);

            _controller.QuitButton.RemoveListenerSafe(OnClickQuitButton);
            _controller.PauseButton.RemoveListenerSafe(OnPauseButtonClicked);
            _controller.ResumeButton.RemoveListenerSafe(OnResumeButtonClicked);
        }

        #endregion


        #region UI - General

        private void OnClickQuitButton()
        {
            _controller.PlayerManager.ClearGuiRoster();

            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.QuitMatch
            });
        }

        private void OpenAndClosePausePanel(bool value) => SetPanelVisible(_controller.PausePanel, value);

        private void OnPauseButtonClicked() => OpenAndClosePausePanel(true);

        private void OnResumeButtonClicked() => OpenAndClosePausePanel(false);

        #endregion


        #region Client Input - Draw / Pass

        private void OnDrawButtonClicked()
        {
            if (!_controller.TurnManager.IsMyTurn())
                return;

            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.DrawCard
            });
        }

        private void OnPassButtonClicked()
        {
            if (!_controller.HandController.AwaitingDrawnCardDecision)
                return;

            _controller.HandController.AwaitingDrawnCardDecision = false;

            _controller.GameButtonManager.SetPassButtonInteractable(false);

            _controller.HandController.RefreshHandInteractability(false);

            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.PassTurn
            });
        }

        private void OnLastCardButtonClicked()
        {
            if (!_controller.TurnManager.IsMyTurn())
                return;

            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.CallLastCard
            });

            _controller.GameButtonManager.SetCallOutButtonInteractable(false);
        }

        #endregion


        #region Client Input - Wild Color

        internal void ShowColorPicker(Action<CardColor> onColorChosen, Card pendingCard)
        {
            _onColorChosen = onColorChosen;
            _pendingWildCard = pendingCard;

            SetPanelVisible(_controller.ColorPickerPanel, true);
        }

        private void OnRedColorClicked() => HandleColorPicked(CardColor.Red);

        private void OnYellowColorClicked() => HandleColorPicked(CardColor.Yellow);

        private void OnGreenColorClicked() => HandleColorPicked(CardColor.Green);

        private void OnBlueColorClicked() => HandleColorPicked(CardColor.Blue);

        internal void HandleColorPicked(CardColor color)
        {
            SetPanelVisible(_controller.ColorPickerPanel, false);

            var callback = _onColorChosen;

            _onColorChosen = null;
            _pendingWildCard = null;

            callback?.Invoke(color);
        }

        /// <summary>
        /// Cancels an in-flight wild-color choice (e.g. because the turn moved on,
        /// or the match ended), hiding the picker panel. Optionally also cancels
        /// the pending card play animation that triggered the picker.
        /// </summary>
        internal void CancelPendingColorPicker(bool cancelPendingCardPlay)
        {
            if (_onColorChosen == null)
                return;

            _onColorChosen = null;
            SetPanelVisible(_controller.ColorPickerPanel, false);

            if (cancelPendingCardPlay)
            {
                _pendingWildCard?.CancelPendingPlay();
                _pendingWildCard = null;
            }
        }

        #endregion


        #region Panel Animation

        internal void SetPanelVisible(GameObject panel, bool visible)
        {
            if (panel == null)
                return;

            if (panel.transform is not RectTransform rect)
                return;

            if (_panelAnimations.TryGetValue(panel, out var running) && running != null)
                _controller.StopCoroutine(running);

            if (visible)
            {
                rect.SetAsLastSibling();
                _panelAnimations[panel] = _controller.StartCoroutine(PopInPanel(rect));
            }
            else
            {
                _panelAnimations[panel] = _controller.StartCoroutine(PopOutPanel(rect));
            }
        }

        private IEnumerator PopInPanel(RectTransform rect)
        {
            const float overshootScale = 1.05f;
            const float scaleUpDuration = 0.2f;
            const float scaleDownDuration = 0.2f;

            rect.localScale = Vector3.zero;

            var elapsed = 0f;

            while (elapsed < scaleUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = Mathf.Clamp01(elapsed / scaleUpDuration);

                rect.localScale = Vector3.one * Mathf.LerpUnclamped(0f, overshootScale, t);

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
        }

        private IEnumerator PopOutPanel(RectTransform rect)
        {
            const float scaleDownDuration = 0.2f;

            var startScale = rect.localScale;
            var elapsed = 0f;

            while (elapsed < scaleDownDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = Mathf.Clamp01(elapsed / scaleDownDuration);

                rect.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, t);

                yield return null;
            }

            rect.localScale = Vector3.zero;
        }

        #endregion
    }
}
