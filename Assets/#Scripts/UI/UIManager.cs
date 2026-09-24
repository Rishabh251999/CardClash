using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardClash
{
    public class UIManager : MonoBehaviour
    {
        #region UI References

        [SerializeField] private List<PanelEntry> _panels;
        [SerializeField] private float _fadeDuration = 0.25f;
        [SerializeField] private float _fadeInDelay = 0.1f;

        #endregion

        #region Runtime State

        public ScreenType ScreenType { get; private set; }

        public event Action<ScreenType> OnStateChanged;

        private readonly Dictionary<CanvasGroup, Coroutine> _activeFades = new();

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (Application.isBatchMode)
                return;

            var hasSavedUserName = !string.IsNullOrWhiteSpace(PlayerPrefs.GetString("UserName", string.Empty));

            if(hasSavedUserName)
            {
                SetState(ScreenType.Loading, instant: true);
            }

            else
            {
                SetState(ScreenType.Login, instant: true);
            }
        }

        #endregion

        public void SetState(ScreenType state, bool instant = false)
        {
            ScreenType = state;

            Debug.Log($"UIManager: SetState({state}, instant: {instant})");

            foreach (var entry in _panels)
            {
                if (entry.CanvasGroup == null)
                    continue;

                var visible = entry.ScreenType == state;

                if (instant)
                {
                    StopExistingFade(entry.CanvasGroup);
                    ApplyImmediate(entry.CanvasGroup, visible);
                    continue;
                }

                StopExistingFade(entry.CanvasGroup);

                if (visible)
                    _activeFades[entry.CanvasGroup] = StartCoroutine(FadeInDelayed(entry.CanvasGroup));
                else
                    _activeFades[entry.CanvasGroup] = StartCoroutine(FadeCanvasGroup(entry.CanvasGroup, false));
            }

            OnStateChanged?.Invoke(state);
        }

        private void StopExistingFade(CanvasGroup group)
        {
            if (_activeFades.TryGetValue(group, out var running) && running != null)
                StopCoroutine(running);
        }

        private void ApplyImmediate(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private IEnumerator FadeInDelayed(CanvasGroup group)
        {
            yield return FadeCanvasGroup(group, true);
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, bool visible)
        {
            yield return new WaitForSeconds(_fadeInDelay);

            var startAlpha = group.alpha;
            var targetAlpha = visible ? 1f : 0f;

            if (visible)
                group.blocksRaycasts = true;
            else
                group.interactable = false;

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            group.alpha = targetAlpha;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            _activeFades[group] = null;
        }
    }
}