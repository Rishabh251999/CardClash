using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardClash
{
    public class InstructionsScreenManager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region Scripts References
        [SerializeField] SettingsSO _settings; 
        [SerializeField] UIManager _uiManager;

        #endregion

        #region UI References

        [Space(5)]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _description1Text;
        [SerializeField] private TextMeshProUGUI _description2Text;

        #endregion

        #region Sprite References

        [Space(5)]
        [SerializeField] private Sprite _dotOnSprite;
        [SerializeField] private Sprite _dotOffSprite;

        #endregion

        #region Runtime Collections

        [Space(5)]
        [SerializeField] private InstructionsScreenData[] _view;

        [Space(2.5f)]
        [SerializeField] private Image[] _pageDots;

        [Space(2.5f)]
        [SerializeField] private CanvasGroup[] _screens;

        [Space(2.5f)]
        [SerializeField] private RectTransform _viewport;

        private Vector2[] _screenPositions;
        private RectTransform[] _screenTransforms;

        #endregion

        #region GameObject References

        [SerializeField] private GameObject _backButtonGameObject;
        [SerializeField] private GameObject _skipButtonGameObject;

        #endregion

        #region Animation Settings

        [Header("Animation Settings")]
        [SerializeField] private float _animationDuration = 0.3f;
        [SerializeField, Range(0.1f, 0.5f)]
        private float _swipeThreshold = 0.25f;

        [SerializeField] private Ease _animationEase = Ease.OutCubic;

        #endregion

        #region Runtime State

        private int _currentScreen;
        private int _targetScreen = -1;

        private bool _isDragging;
        private bool _isAnimating;

        private float _screenWidth;
        private float _dragDistance;

        private Vector2 _pointerDownPosition;

        private Sequence _screenSequence;

        private Button _backButton;
        private Button _skipButton;

        private CanvasGroup _backButtonCanvasGroup;
        private CanvasGroup _skipButtonCanvasGroup;


        #endregion

        #region Unity LifeCycle

        private void Awake()
        {
            int screenCount = _screens.Length;

            _screenTransforms = new RectTransform[screenCount];
            _screenPositions = new Vector2[screenCount];

            for (int i = 0; i < screenCount; i++)
            {
                _screenTransforms[i] = _screens[i].GetComponent<RectTransform>();

                _screenPositions[i] = _screenTransforms[i].anchoredPosition;
            }

            _backButtonCanvasGroup = _backButtonGameObject.GetComponent<CanvasGroup>();
            _skipButtonCanvasGroup = _skipButtonGameObject.GetComponent<CanvasGroup>();

            _backButton = _backButtonGameObject.GetComponent<Button>();
            _skipButton = _skipButtonGameObject.GetComponent<Button>();

            _screenWidth = _viewport.rect.width;
        }

        private void Start()
        {
            _backButton.AddListenerSafe(OnClickBackButton);
            _skipButton.AddListenerSafe(OnClickSkipButton);

            _uiManager.OnStateChanged += OnScreenChanged;

            ShowScreen(0);
        }

        private void OnDisable()
        {
            _backButton.RemoveListenerSafe(OnClickBackButton);
            _skipButton.RemoveListenerSafe(OnClickSkipButton);

            _uiManager.OnStateChanged -= OnScreenChanged;

            _screenSequence?.Kill();

            _isDragging = false;
            _isAnimating = false;

            if (_screenTransforms != null)
                ShowScreen(_currentScreen);
        }

        private void OnClickBackButton() => _uiManager.SetState(ScreenType.Settings);

        private void OnClickSkipButton() => _uiManager.SetState(ScreenType.Loading);

        private void OnScreenChanged(ScreenType screenType)
        {
            var (showBack, showSkip) = screenType switch
            {
                ScreenType.Login => (false, true),
                ScreenType.Settings => (true, false),
                _ => (false, false)
            };

            Debug.Log($"Screen opened: {screenType}. Show Back: {showBack}, Show Skip: {showSkip}");    

            SetCanvasGroup(_backButtonCanvasGroup, showBack);
            SetCanvasGroup(_skipButtonCanvasGroup, showSkip);
        }

        private void SetCanvasGroup(CanvasGroup canvasGroup, bool isVisible)
        {
            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }

        #endregion

        #region Screen Navigation

        private void ShowScreen(int index)
        {
            if (index < 0 || index >= _screens.Length || index >= _view.Length)
                return;

            _currentScreen = index;

            for (var i = 0; i < _screens.Length; i++)
            {
                var isActive = i == index;

                _screens[i].alpha = isActive ? 1f : 0f;
                _screens[i].interactable = isActive;
                _screens[i].blocksRaycasts = isActive;

                _screenTransforms[i].anchoredPosition = _screenPositions[i];
            }

            UpdateScreenContent();
            UpdatePageDots();
        }

        #endregion


        #region Drag Controls

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isAnimating)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position,eventData.pressEventCamera,
                out _pointerDownPosition))
                return;

            _isDragging = true;
            _dragDistance = 0f;
            _targetScreen = -1;

            _screenWidth = _viewport.rect.width;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _isAnimating)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, eventData.pressEventCamera,
                out Vector2 currentPosition))
                return;

            _dragDistance = currentPosition.x - _pointerDownPosition.x;

            var nextIndex = _dragDistance < 0 ? _currentScreen + 1 : _currentScreen - 1;

            RectTransform current =
                _screenTransforms[_currentScreen];

            Vector2 currentBase =
                _screenPositions[_currentScreen];

            // First / Last screen resistance
            if (nextIndex < 0 || nextIndex >= _screens.Length)
            {
                _targetScreen = -1;

                current.anchoredPosition =
                    currentBase +
                    Vector2.right * (_dragDistance * 0.25f);

                return;
            }

            _targetScreen = nextIndex;

            RectTransform next =
                _screenTransforms[nextIndex];

            Vector2 nextBase =
                _screenPositions[nextIndex];

            float distance = Mathf.Clamp(
                _dragDistance,
                -_screenWidth,
                _screenWidth
            );

            float direction = Mathf.Sign(distance);

            // Current screen follows finger
            current.anchoredPosition =
                currentBase + Vector2.right * distance;

            // Incoming screen follows from opposite side
            next.anchoredPosition =
                nextBase +
                Vector2.right * (distance - direction * _screenWidth);

            _screens[nextIndex].alpha = 1f;
            _screens[nextIndex].interactable = false;
            _screens[nextIndex].blocksRaycasts = false;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging)
                return;

            _isDragging = false;

            float threshold = _screenWidth * _swipeThreshold;

            if (_targetScreen >= 0 &&
                Mathf.Abs(_dragDistance) >= threshold)
            {
                AnimateScreen(_targetScreen);
            }
            else
            {
                SnapBack();
            }
        }

        #endregion


        #region Screen Animation

        private void AnimateScreen(int nextIndex)
        {
            if (_isAnimating ||
                nextIndex < 0 ||
                nextIndex >= _screens.Length ||
                nextIndex >= _view.Length)
                return;

            _isAnimating = true;

            int previousIndex = _currentScreen;

            RectTransform current =
                _screenTransforms[previousIndex];

            RectTransform next =
                _screenTransforms[nextIndex];

            Vector2 currentPosition =
                _screenPositions[previousIndex];

            Vector2 nextPosition =
                _screenPositions[nextIndex];

            float direction = nextIndex > previousIndex ? -1f : 1f;

            // Prepare incoming screen if this is button navigation
            if (_screens[nextIndex].alpha == 0f)
            {
                next.anchoredPosition =
                    nextPosition +
                    Vector2.right * -direction * _screenWidth;
            }

            _screens[nextIndex].alpha = 1f;
            _screens[nextIndex].interactable = false;
            _screens[nextIndex].blocksRaycasts = false;

            _screens[previousIndex].interactable = false;
            _screens[previousIndex].blocksRaycasts = false;

            _screenSequence = DOTween.Sequence();

            _screenSequence.Join(
                current.DOAnchorPos(
                    currentPosition +
                    _screenWidth * direction * Vector2.right,
                    _animationDuration
                )
            );

            _screenSequence.Join(
                next.DOAnchorPos(
                    nextPosition,
                    _animationDuration
                )
            );

            _screenSequence.SetEase(_animationEase);

            _screenSequence.OnComplete(() =>
            {
                _screens[previousIndex].alpha = 0f;

                current.anchoredPosition = currentPosition;

                _currentScreen = nextIndex;

                _targetScreen = -1;

                UpdateScreenContent();
                UpdatePageDots();

                _screens[nextIndex].interactable = true;
                _screens[nextIndex].blocksRaycasts = true;

                _isAnimating = false;
            });
        }

        private void SnapBack()
        {
            _isAnimating = true;

            int currentIndex = _currentScreen;

            RectTransform current =
                _screenTransforms[currentIndex];

            _screenSequence = DOTween.Sequence();

            _screenSequence.Join(
                current.DOAnchorPos(
                    _screenPositions[currentIndex],
                    _animationDuration
                )
            );

            if (_targetScreen >= 0)
            {
                int nextIndex = _targetScreen;

                _screenSequence.Join(
                    _screenTransforms[nextIndex].DOAnchorPos(
                        _screenPositions[nextIndex] +
                        Vector2.right *
                        (_dragDistance < 0
                            ? _screenWidth
                            : -_screenWidth),
                        _animationDuration
                    )
                );
            }

            _screenSequence.SetEase(_animationEase);

            _screenSequence.OnComplete(() =>
            {
                if (_targetScreen >= 0)
                {
                    _screens[_targetScreen].alpha = 0f;

                    _screenTransforms[_targetScreen].anchoredPosition =
                        _screenPositions[_targetScreen];
                }

                _targetScreen = -1;
                _isAnimating = false;
            });
        }

        #endregion


        #region Screen Content

        private void UpdateScreenContent()
        {
            _titleText.text = _view[_currentScreen].title;

            _description1Text.text =
                _view[_currentScreen].description1;

            _description2Text.text =
                _view[_currentScreen].description2;
        }

        private void UpdatePageDots()
        {
            for (int i = 0; i < _pageDots.Length; i++)
            {
                _pageDots[i].sprite =
                    i == _currentScreen
                    ? _dotOnSprite
                    : _dotOffSprite;
            }
        }

        #endregion
    }

    [Serializable]
    public struct InstructionsScreenData
    {
        public string title;
        public string description1;
        public string description2;
    }
}
