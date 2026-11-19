
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    [RequireComponent(typeof(Image))]
    public class ReverseCard : MonoBehaviour
    {
        #region UI References

        [SerializeField] private CanvasGroup _canvasGroup;

        #endregion

        #region Settings

        [SerializeField] private float _rotationSpeed = 90f;
        [SerializeField] private float _colorTransitionDuration = 0.35f;

        #endregion

        #region Runtime References

        private Image _image;
        private Tween _rotationTween;
        private Tween _colorTween;

        private int _clockwise = 1;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void OnDisable()
        {
            StopRotating();
        }

        #endregion

        #region Rotation

        public void StartRotating(Color tint)
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;

            SetColor(tint);
            RestartRotationIfNeeded();
        }

        private void RestartRotationIfNeeded()
        {
            if (_rotationTween is { active: true })
                return;

            var duration = 360f / Mathf.Max(1f, _rotationSpeed);

            _rotationTween = transform.DOLocalRotate(new Vector3(0f, 0f, -_clockwise * 360f),duration,
                RotateMode.LocalAxisAdd).SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental)
                .SetUpdate(true);
        }

        public void StopRotating()
        {
            _rotationTween?.Kill();
            _rotationTween = null;

            _colorTween?.Kill();
            _colorTween = null;

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        public void SetDirection(int clockwise)
        {
            clockwise = clockwise >= 0 ? 1 : -1;

            if (_clockwise == clockwise)
                return;

            _clockwise = clockwise;

            if (_rotationTween is not { active: true })
                return;

            _rotationTween.Kill();
            _rotationTween = null;

            RestartRotationIfNeeded();
        }

        #endregion

        #region Color

        public void SetColor(Color tint)
        {
            _colorTween?.Kill();

            _colorTween = _image
                .DOColor(tint, _colorTransitionDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        #endregion
    }
}
