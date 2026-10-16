using Mirror;
using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace CardClash
{
    [RequireComponent(typeof(Image), typeof(Button))]
    [RequireComponent(typeof(LayoutElement))]
    public class Card : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region constants/readonly

        private const float DragFollowRate = 28f;        // higher = tighter follow
        private const float DragTiltMax = 25f;
        private const float DragTiltFactor = 0.08f;
        private const float DragTiltSmoothing = 14f;
        private const float SettleDuration = 0.35f;
        private const float SquashDuration = 0.18f;

        #endregion

        #region Inspector

        [Tooltip("How long to wait for the server to accept a play before the card goes back to the hand.")]
        [SerializeField] private float _returnWait = 0.6f;

        [Tooltip("A drop above this fraction of the screen height also counts as a play (0 = only the target area counts).")]
        [SerializeField, Range(0f, 1f)] private float _dropHeightFraction = 0.4f;

        #endregion

        #region UI References

        private Image _cardImage;
        private Button _button;
        private RectTransform _rectTransform;
        private LayoutElement _layoutElement;
        private Canvas _rootCanvas;

        #endregion

        #region Runtime Collections

        public static readonly Dictionary<byte, Sprite> CardSprites = new();

        #endregion

        #region Runtime State

        public GameCard CardData;

        private Vector3 _dealTargetLocalPos;

        // drag state
        private RectTransform _placeholder;      // empty slot that keeps the hand layout unchanged while dragging
        private Transform _handParent;
        private Vector3 _dragOffset;
        private Vector3 _dragTargetWorld;
        private Vector3 _lastWorldPos;
        private float _tilt;

        private bool _isDragging;
        private bool _pendingPlay;               // dropped on the table, waiting for the server
        private bool _played;                    // the server accepted the play

        #endregion

        #region Unity Methods

        private void Awake()
        {
            _cardImage = GetComponent<Image>();
            _button = GetComponent<Button>();
            _rectTransform = (RectTransform)transform;
            _layoutElement = GetComponent<LayoutElement>();
            _rootCanvas = GetComponentInParent<Canvas>();
        }

        private void OnDestroy()
        {
            // the hand was rebuilt while the card was being dragged
            if (_placeholder != null) Destroy(_placeholder.gameObject);
        }

        private void TryPlay()
        {
            if (NetworkClient.localPlayer is not { } localPlayer ||
                !localPlayer.TryGetComponent<CardPlayerController>(out var playerController))
                return;

            playerController.TryPlayCard(this);
        }

        public void SetInteractable(bool canPlay)
        {
            _button.interactable = canPlay;

            // the turn ended while the card was in the player's hand: let go of it
            if (!canPlay && _isDragging)
            {
                _isDragging = false;
                StartCoroutine(SettleBackToHand());
            }
        }

        /// <summary>Call this if the server rejects a play, so the card goes back to the hand at once.</summary>
        public void CancelPendingPlay()
        {
            if (!_pendingPlay || _played) return;
            StopAllCoroutines();
            StartCoroutine(SettleBackToHand());
        }

        #endregion

        #region Drag Handling

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_button.interactable || _played || _pendingPlay || _isDragging || _placeholder != null)
                return;

            if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>();
            if (_rootCanvas == null) return;

            var canvasRect = (RectTransform)_rootCanvas.rootCanvas.transform;
            var size = _rectTransform.rect.size;
            var worldPos = _rectTransform.position;

            _handParent = transform.parent;

            // 1) leave an empty slot of the same size, so the other cards stay exactly where they are
            _placeholder = new GameObject("CardPlaceholder", typeof(RectTransform), typeof(LayoutElement))
                .GetComponent<RectTransform>();
            _placeholder.SetParent(_handParent, false);
            _placeholder.SetSiblingIndex(transform.GetSiblingIndex());
            _placeholder.sizeDelta = size;

            var le = _placeholder.GetComponent<LayoutElement>();
            le.preferredWidth = _layoutElement.preferredWidth >= 0f ? _layoutElement.preferredWidth : size.x;
            le.preferredHeight = _layoutElement.preferredHeight >= 0f ? _layoutElement.preferredHeight : size.y;
            le.minWidth = _layoutElement.minWidth;
            le.minHeight = _layoutElement.minHeight;
            le.flexibleWidth = _layoutElement.flexibleWidth;
            le.flexibleHeight = _layoutElement.flexibleHeight;

            // 2) lift the real card to the top of the canvas, so it draws above all other cards
            transform.SetParent(canvasRect, true);
            transform.SetAsLastSibling();
            _rectTransform.anchorMin = _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.sizeDelta = size;
            _rectTransform.position = worldPos;

            // 3) remember where the finger is on the card, so it doesn't jump to the center
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect, eventData.position, eventData.pressEventCamera, out var fingerWorld);
            _dragOffset = _rectTransform.position - fingerWorld;
            _dragTargetWorld = _lastWorldPos = _rectTransform.position;
            _tilt = 0f;

            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _rootCanvas == null) return;

            var canvasRect = (RectTransform)_rootCanvas.rootCanvas.transform;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvasRect, eventData.position, eventData.pressEventCamera, out var fingerWorld))
            {
                _dragTargetWorld = fingerWorld + _dragOffset;
            }
        }

        // follow and tilt run every frame, so the tilt relaxes when the finger stops moving
        private void Update()
        {
            if (!_isDragging) return;

            // the hand was rebuilt by the server while dragging: the slot is gone, so drop the card
            if (_placeholder == null)
            {
                Destroy(gameObject);
                return;
            }

            float dt = Time.unscaledDeltaTime;

            var pos = Vector3.Lerp(_rectTransform.position, _dragTargetWorld, 1f - Mathf.Exp(-DragFollowRate * dt));
            _rectTransform.position = pos;

            float scale = Mathf.Max(0.01f, _rootCanvas.scaleFactor);
            float velocityX = (pos.x - _lastWorldPos.x) / Mathf.Max(dt, 0.0001f) / scale;
            _lastWorldPos = pos;

            float targetTilt = Mathf.Clamp(-velocityX * DragTiltFactor, -DragTiltMax, DragTiltMax);
            _tilt = Mathf.Lerp(_tilt, targetTilt, 1f - Mathf.Exp(-DragTiltSmoothing * dt));
            _rectTransform.localRotation = Quaternion.Euler(0f, 0f, _tilt);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;

            if (IsOverDropZone(eventData))
            {
                _pendingPlay = true;
                TryPlay(); 
                return;
            }

            StartCoroutine(SettleBackToHand());
        }

        private bool IsOverDropZone(PointerEventData eventData)
        {
            // anywhere in the upper part of the screen counts, so a slightly off drop is not a miss
            if (_dropHeightFraction > 0f && eventData.position.y > Screen.height * _dropHeightFraction)
                return true;

            if (CardGameController.Instance == null)
                return false;

            if (CardGameController.Instance.CardTargetTransform is not RectTransform dropZoneRect)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(
                dropZoneRect, eventData.position, eventData.pressEventCamera);
        }

        public void BeginReturnWatch()
        {
            if (_played) return;
            StartCoroutine(ReturnIfNotPlayed());
        }

        private IEnumerator ReturnIfNotPlayed()
        {
            yield return new WaitForSecondsRealtime(_returnWait);

            if (_played) yield break;
            yield return SettleBackToHand();
        }

        private IEnumerator SettleBackToHand()
        {
            if (_placeholder == null)
            {
                ReturnToHand();
                yield break;
            }

            var start = _rectTransform.position;
            var startRot = _rectTransform.localRotation;
            float elapsed = 0f;

            while (elapsed < SettleDuration)
            {
                if (_placeholder == null) { Destroy(gameObject); yield break; }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SettleDuration);

                // the slot's position is read every frame, so it stays right even if the hand moves
                _rectTransform.position = Vector3.LerpUnclamped(start, _placeholder.position, EaseOutBack(t));
                _rectTransform.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, t);

                yield return null;
            }

            ReturnToHand();
        }

        private void ReturnToHand()
        {
            int index = transform.GetSiblingIndex();

            if (_placeholder != null)
            {
                index = _placeholder.GetSiblingIndex();
                _placeholder.gameObject.SetActive(false);   // inactive = ignored by the layout at once
                Destroy(_placeholder.gameObject);
                _placeholder = null;
            }

            if (_handParent != null)
            {
                transform.SetParent(_handParent, false);
                transform.SetSiblingIndex(index);
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)_handParent);
            }

            _rectTransform.localRotation = Quaternion.identity;
            _rectTransform.localScale = Vector3.one;
            _pendingPlay = false;
            _isDragging = false;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
        }

        #endregion

        #region Sprites

        public static void PopulateCardSprites()
        {
            CardSprites.Clear();

            // Must match your asset names exactly
            var colors = new string[] { "Red", "Green", "Blue", "Yellow" };

            byte id = 0;

            foreach (var color in colors)
            {
                // One zero
                CardSprites[id++] = Load($"{color}_0");

                // Two each of 1-9
                for (int n = 1; n <= 9; n++)
                {
                    var s = Load($"{color}_{n}");
                    CardSprites[id++] = s;
                    CardSprites[id++] = s;
                }

                // Two each of Draw, Reverse, Skip
                for (int i = 0; i < 2; i++)
                {
                    CardSprites[id++] = Load($"{color}_Skip");
                    CardSprites[id++] = Load($"{color}_Reverse");
                    CardSprites[id++] = Load($"{color}_Draw");
                }
            }

            // Four Wild, four Wild Draw Four
            var wild = Load("Wild");
            var wildFour = Load("WildDrawFour");

            for (int i = 0; i < 4; i++)
            {
                CardSprites[id++] = wild;
                CardSprites[id++] = wildFour;
            }
        }

        private static Sprite Load(string name)
        {
            var s = Resources.Load<Sprite>($"Cards/{name}");
            if (s == null) Debug.LogWarning($"[CardView] Sprite not found: Cards/{name}");
            return s;
        }

        public void Setup(GameCard card)
        {
            CardData = card;

            if (CardSprites.TryGetValue(card.Id, out Sprite sprite))
                _cardImage.sprite = sprite;
            else
                Debug.LogWarning($"[CardView] No sprite for card Id {card.Id} ({card})");
        }

        #endregion

        #region Play animation

        public void PlayTowards(Transform target, Transform canvas)
        {
            // the server accepted the play: stop everything that could bring the card back
            _played = true;
            _pendingPlay = false;
            _isDragging = false;
            StopAllCoroutines();

            if (_placeholder != null)
            {
                _placeholder.gameObject.SetActive(false);
                Destroy(_placeholder.gameObject);
                _placeholder = null;
            }

            _button.interactable = false;

            if (target == null)
                return;

            transform.SetParent(canvas, true);

            _rectTransform.sizeDelta = new Vector2(150f, 217f);

            StartCoroutine(MoveToTarget(target));
        }

        private IEnumerator MoveToTarget(Transform target)
        {
            var startWorldPosition = _rectTransform.position;
            var targetWorldPosition = target.position;

            float duration = CardGameController.Instance.PlayMoveDuration;
            float elapsed = 0f;

            float startRotZ = _rectTransform.localEulerAngles.z;
            if (startRotZ > 180f) startRotZ -= 360f;       // shortest way back to 0

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutBack(t);

                _rectTransform.position = Vector3.LerpUnclamped(startWorldPosition, targetWorldPosition, eased);
                _rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(startRotZ, 0f, t));

                yield return null;
            }

            _rectTransform.position = targetWorldPosition;

            yield return StartCoroutine(SquashAndSettle());

            Destroy(gameObject);
        }

        private IEnumerator SquashAndSettle()
        {
            var randomTilt = UnityEngine.Random.Range(-15f, 15f);
            var squash = new Vector3(1.15f, 0.85f, 1f);
            var normal = Vector3.one;

            float elapsed = 0f;
            while (elapsed < SquashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SquashDuration);

                _rectTransform.localScale = Vector3.Lerp(squash, normal, t);

                yield return null;
            }

            _rectTransform.localScale = normal;
            _rectTransform.localRotation = Quaternion.Euler(0f, 0f, randomTilt);
        }

        #endregion

        #region Deal animation

        public void CaptureDealTarget()
        {
            _dealTargetLocalPos = _rectTransform.localPosition;
        }

        public void PrepareDeal(Transform origin)
        {
            _button.interactable = false;
            _layoutElement.ignoreLayout = true;

            if (origin is { })
                transform.position = origin.position;
        }

        public void AnimateDeal()
        {
            StartCoroutine(MoveFromOrigin(_dealTargetLocalPos, transform.parent));
        }

        private IEnumerator MoveFromOrigin(Vector3 targetLocalPos, Transform parent)
        {
            Vector3 startPos = transform.position;
            Vector3 targetWorldPos = parent.TransformPoint(targetLocalPos);
            float duration = CardGameController.Instance.PlayMoveDuration;

            float startSpin = UnityEngine.Random.Range(180f, 360f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutBack(t);

                transform.position = Vector3.LerpUnclamped(startPos, targetWorldPos, eased);
                _rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(startSpin, 0f, t));

                yield return null;
            }

            transform.localPosition = targetLocalPos;
            _rectTransform.localRotation = Quaternion.identity;
            _layoutElement.ignoreLayout = false;
            _button.interactable = true;
        }

        #endregion
    }
}