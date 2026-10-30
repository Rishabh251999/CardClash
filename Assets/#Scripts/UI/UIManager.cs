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

        [Header("Scale Transition")]
        [Tooltip("Starting/ending scale for a panel when it is hidden (1 = no scale effect).")]
        [SerializeField, Range(0.5f, 1f)] private float _hiddenScale = 0.95f;

        #endregion

        #region Runtime State

        public ScreenType ScreenType { get; private set; }

        public event Action<ScreenType> OnStateChanged;
        public event Action<ScreenType> OnScreenOpened;

        private readonly Dictionary<CanvasGroup, Coroutine> _activeFades = new();

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (Application.isBatchMode)
                return;

            var hasSavedUserName = !string.IsNullOrWhiteSpace(PlayerPrefs.GetString("UserName", string.Empty));

            if (hasSavedUserName)
            {
                SetState(ScreenType.Loading, false);
            }

            else
            {
                SetState(ScreenType.Login, false);
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

                    if (visible)
                        OnScreenOpened?.Invoke(entry.ScreenType);

                    continue;
                }

                StopExistingFade(entry.CanvasGroup);

                if (visible)
                    _activeFades[entry.CanvasGroup] = StartCoroutine(FadeInDelayed(entry.CanvasGroup, entry.ScreenType));
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
            group.transform.localScale = visible ? Vector3.one : Vector3.one * _hiddenScale;
        }

        private IEnumerator FadeInDelayed(CanvasGroup group, ScreenType screenType)
        {
            yield return FadeCanvasGroup(group, true);

            OnScreenOpened?.Invoke(screenType);
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, bool visible)
        {
            yield return new WaitForSeconds(_fadeInDelay);

            var startAlpha = group.alpha;
            var targetAlpha = visible ? 1f : 0f;

            var startScale = group.transform.localScale;
            var targetScale = visible ? Vector3.one : Vector3.one * _hiddenScale;

            if (visible)
                group.blocksRaycasts = true;
            else
                group.interactable = false;

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
                group.transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);

                yield return null;
            }

            group.alpha = targetAlpha;
            group.transform.localScale = targetScale;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            _activeFades[group] = null;
        }
    }
}