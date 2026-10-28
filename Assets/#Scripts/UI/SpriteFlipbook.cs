using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    /// <summary>
    /// Plays a sequence of sprites on a UI Image (frame-by-frame animation).
    /// Use it with reverse_fx_00 ... reverse_fx_23.
    ///
    /// Setup: Image (Raycast Target OFF) centered on the discard pile, ~1.3x card height.
    /// Drag all 24 frames into "Frames" (or select them, then drag onto the array label).
    /// Call Play(tint) when a Reverse card is played.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SpriteFlipbook : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fps = 30f;
        [SerializeField] private bool _keepLastFrame = true;   // stay on the final (mirrored ring) frame
        [SerializeField] private bool _playOnEnable;

        public float Duration => _frames != null && _frames.Length > 0
            ? _frames.Length / Mathf.Max(1f, _fps)
            : 0f;

        public event Action Finished;

        private Image _image;
        private Coroutine _routine;

        private void Awake() => _image = GetComponent<Image>();
        private void OnEnable() { if (_playOnEnable) Play(_image.color); }

        public void Play(Color tint)
        {
            _canvasGroup.alpha = 1f;

            if (_frames == null || _frames.Length == 0) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(tint));
        }

        public void SetDirection(int direction)
        {
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction == 0 ? 1 : direction);
            transform.localScale = scale;
        }

        private IEnumerator Run(Color tint)
        {
            _image.enabled = true;
            _image.color = tint;                      // frames are white, so Image.color tints them
            var step = 1f / Mathf.Max(1f, _fps);

            for (var i = 0; i < _frames.Length; i++)
            {
                _image.sprite = _frames[i];
                yield return new WaitForSecondsRealtime(step);   // unscaled: works while paused
            }

            if (!_keepLastFrame) _image.enabled = false;
            _routine = null;
            Finished?.Invoke();

            _canvasGroup.alpha = 0f;  
        }
    }
}
