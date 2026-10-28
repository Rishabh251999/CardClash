using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    /// <summary>
    /// Controls only the squash animation and sprite swap of a toggle's visual
    /// (no SFX / status text logic). Reusable across different UI screens.
    /// </summary>
    [Serializable]
    public class ToggleAnimator
    {
        [Header("Runtime Settings")]
        [Space(5f)]

        [SerializeField, Min(0f)]
        private float _toggleAnimDuration = 0.12f;

        [SerializeField, Range(0.1f, 1f)]
        private float _toggleSquashScale = 0.85f;

        private readonly MonoBehaviour _owner;
        private readonly Toggle _toggle;
        private readonly Sprite _onSprite;
        private readonly Sprite _offSprite;

        private Vector3 _originalScale;
        private Coroutine _animation;

        public ToggleAnimator(MonoBehaviour owner, Toggle toggle, Sprite onSprite, Sprite offSprite)
        {
            _owner = owner;
            _toggle = toggle;
            _onSprite = onSprite;
            _offSprite = offSprite;

            _originalScale = toggle.image.rectTransform.localScale;
        }

        public void SetVisualInstant()
        {
            _toggle.image.sprite = _toggle.isOn ? _onSprite : _offSprite;
        }

        public void Play()
        {
            StopAnimation();

            if (_toggleAnimDuration <= 0f)
            {
                SetVisualInstant();
            }
            else
            {
                _animation = _owner.StartCoroutine(AnimateToggle());
            }
        }

        public void StopAnimation()
        {
            if (_animation != null)
            {
                _owner.StopCoroutine(_animation);
                _animation = null;
            }

            if (_toggle.image != null)
            {
                _toggle.image.rectTransform.localScale = _originalScale;
            }
        }

        private IEnumerator AnimateToggle()
        {
            var rect = _toggle.image.rectTransform;

            var squashedScale = _originalScale * _toggleSquashScale;

            var halfDuration = _toggleAnimDuration * 0.5f;
            var elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;

                rect.localScale = Vector3.Lerp(_originalScale, squashedScale, Mathf.Clamp01(elapsed / halfDuration));

                yield return null;
            }

            rect.localScale = squashedScale;

            _toggle.image.sprite = _toggle.isOn ? _onSprite : _offSprite;

            elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;

                rect.localScale = Vector3.Lerp(squashedScale, _originalScale, Mathf.Clamp01(elapsed / halfDuration));

                yield return null;
            }

            rect.localScale = _originalScale;

            _animation = null;
        }
    }
}