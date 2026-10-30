using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    /// <summary>
    /// Controls only the squash animation and sprite swap of a toggle's visual
    /// (no SFX / status text logic). Reusable across different UI screens.
    /// </summary>
    public class ToggleAnimator
    {
        [Header("Runtime Settings")]
        [Space(5f)]

        [SerializeField, Min(0f)]
        private float _toggleAnimDuration = 0.12f;

        [SerializeField, Range(0.1f, 1f)]
        private float _toggleSquashScale = 0.85f;

        private readonly Toggle _toggle;
        private readonly Sprite _onSprite;
        private readonly Sprite _offSprite;

        private Vector3 _originalScale;
        private Sequence _animation;

        public ToggleAnimator(MonoBehaviour owner, Toggle toggle, Sprite onSprite, Sprite offSprite)
        {
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
                return;
            }

            var rect = _toggle.image.rectTransform;
            var squashedScale = _originalScale * _toggleSquashScale;
            var halfDuration = _toggleAnimDuration * 0.5f;

            _animation = DOTween.Sequence()
                .Append(rect.DOScale(squashedScale, halfDuration))
                .AppendCallback(() => _toggle.image.sprite = _toggle.isOn ? _onSprite : _offSprite)
                .Append(rect.DOScale(_originalScale, halfDuration))
                .OnComplete(() => _animation = null);
        }

        public void StopAnimation()
        {
            if (_animation != null)
            {
                _animation.Kill();
                _animation = null;
            }

            if (_toggle.image != null)
            {
                _toggle.image.rectTransform.localScale = _originalScale;
            }
        }
    }
}