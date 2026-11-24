using System;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
        [SerializeField] private InstructionsScreenData[] _instructionScreens;

        [Space(2.5f)]
        [SerializeField] private RectTransform _viewport;

        private Vector2[] _screenPositions;
        private RectTransform[] _screenTransforms;

        #endregion

        #region GameObject References

        [Space(5f)]
        [SerializeField] private GameObject _backButtonGameObject;
        [SerializeField] private GameObject _skipButtonGameObject;
        [SerializeField] private GameObject _letsPlayButtonGameObject;

        #endregion

        #region Animation Settings

        [Header("Animation Settings")]
        [SerializeField] private float _animationDuration = 0.3f;
        [SerializeField, Range(0.1f, 0.5f)]
        private float _swipeThreshold = 0.12f;

        [SerializeField] private Ease _animationEase = Ease.OutCubic;

        #endregion

        #region Runtime State

        private int _currentScreen;
        private int _targetScreen = -1;
        private int _previewedScreen = -1;

        private bool _isDragging;
        private bool _isAnimating;

        private float _screenWidth;
        private float _dragDistance;

        private Vector2 _pointerDownPosition;

        private Sequence _screenSequence;

        private Button _backButton;
        private Button _skipButton;
        private Button _letsPlayButton;

        private CanvasGroup _backButtonCanvasGroup;
        private CanvasGroup _skipButtonCanvasGroup;
        private CanvasGroup _letsPlayButtonCanvasGroup;


        #endregion

        #region Unity LifeCycle

        private void Awake()
        {
            var screenCount = _instructionScreens.Length;

            _screenTransforms = new RectTransform[screenCount];
            _screenPositions = new Vector2[screenCount];

             for (int i = 0; i < screenCount; i++)
    {
        var screen = _instructionScreens[i].screen;

        _screenTransforms[i] = screen.GetComponent<RectTransform>();
        _screenPositions[i] = _screenTransforms[i].anchoredPosition;
    }

            _backButtonCanvasGroup = _backButtonGameObject.GetComponent<CanvasGroup>();
            _skipButtonCanvasGroup = _skipButtonGameObject.GetComponent<CanvasGroup>();
            _letsPlayButtonCanvasGroup = _letsPlayButtonGameObject.GetComponent<CanvasGroup>();

            _backButton = _backButtonGameObject.GetComponent<Button>();
            _skipButton = _skipButtonGameObject.GetComponent<Button>();
            _letsPlayButton = _letsPlayButtonGameObject.GetComponent<Button>();

            _screenWidth = _viewport.rect.width;
        }

        private void Start()
        {
            _uiManager.OnStateChanged += OnScreenChanged;

            _backButton.AddListenerSafe(OnClickBackButton);
            _skipButton.AddListenerSafe(OnClickSkipButton);
            _letsPlayButton.AddListenerSafe(OnClickSkipButton);

            ShowScreen(0);
        }

        private void OnDestroy()
        {
            _backButton.RemoveListenerSafe(OnClickBackButton);
            _skipButton.RemoveListenerSafe(OnClickSkipButton);
            _letsPlayButton.RemoveListenerSafe(OnClickSkipButton);

            _uiManager.OnStateChanged -= OnScreenChanged;

            _screenSequence?.Kill();
            _screenSequence = null;

            _isDragging = false;
            _isAnimating = false;
            _targetScreen = -1;

            if (_screenTransforms != null)
                ShowScreen(_currentScreen);
        }

        private void OnClickBackButton() => _uiManager.SetState(ScreenType.Settings);

        private void OnClickSkipButton() => _uiManager.SetState(ScreenType.Loading);

        private void OnScreenChanged(ScreenType screenType)
        {
            if (screenType != ScreenType.Instructions)
                return;

            UpdateNavigationButtons();
        }

        private void UpdateNavigationButtons()
        {
            var isLastScreen = _currentScreen == _instructionScreens.Length - 1;
            var loginScreen = _uiManager.PreviousScreenType == ScreenType.Login;

            var showBack = _uiManager.PreviousScreenType == ScreenType.Settings;
            var showSkip = loginScreen && !isLastScreen;
            var showLetsPlay = loginScreen && isLastScreen;

            SetCanvasGroup(_backButtonCanvasGroup, showBack);
            SetCanvasGroup(_skipButtonCanvasGroup, showSkip);
            SetCanvasGroup(_letsPlayButtonCanvasGroup, showLetsPlay);
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
            if (index < 0 || index >= _instructionScreens.Length)
                return;

            _currentScreen = index;

            for (var i = 0; i < _instructionScreens.Length; i++)
            {
                var isActive = i == index;
                var screen = _instructionScreens[i].screen;

                SetCanvasGroup(screen, isActive);

                _screenTransforms[i].anchoredPosition = _screenPositions[i];
            }

            UpdateScreenContent();
            UpdatePageDots();
        }

        private void UpdatePreviewContent(int index)
        {
            var screenData = _instructionScreens[index];

            _titleText.text = screenData.title;
            _description1Text.text = screenData.description1;
            _description2Text.text = screenData.description2;

            for (int i = 0; i < _instructionScreens.Length; i++)
            {
                var pageDot = _instructionScreens[i].pageDot;

                if (pageDot == null)
                    continue;

                pageDot.sprite = i == index
                    ? _dotOnSprite
                    : _dotOffSprite;
            }
        }

        #endregion


        #region Drag Controls

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isAnimating)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position,
                eventData.pressEventCamera, out _pointerDownPosition))
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

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, 
                eventData.pressEventCamera, out Vector2 currentPosition))
                return;

            _dragDistance = currentPosition.x - _pointerDownPosition.x;

            var nextIndex = _dragDistance < 0 ? _currentScreen + 1 : _currentScreen - 1;

            var current = _screenTransforms[_currentScreen];

            var currentBase = _screenPositions[_currentScreen];

            if (nextIndex < 0 || nextIndex >= _instructionScreens.Length)
            {
                _targetScreen = -1;

                current.anchoredPosition = currentBase + Vector2.right * (_dragDistance * 0.25f);

                return;
            }

            _targetScreen = nextIndex;

            var dragProgress = Mathf.Abs(_dragDistance) / _screenWidth;

            int previewIndex = dragProgress >= 0.5f
                ? nextIndex
                : _currentScreen;

            if (_previewedScreen != previewIndex)
            {
                UpdatePreviewContent(previewIndex);
                _previewedScreen = previewIndex;
            }

            var next = _screenTransforms[nextIndex];

            var nextBase = _screenPositions[nextIndex];

            var distance = Mathf.Clamp(_dragDistance, -_screenWidth, _screenWidth);
            var direction = Mathf.Sign(distance);

            // Current screen follows finger
            current.anchoredPosition = currentBase + Vector2.right * distance;

            // Incoming screen follows from opposite side
            next.anchoredPosition = nextBase + Vector2.right * (distance - direction * _screenWidth);

            _instructionScreens[nextIndex].screen.alpha = 1f;
            _instructionScreens[nextIndex].screen.interactable = false;
            _instructionScreens[nextIndex].screen.blocksRaycasts = false;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging)
                return;

            _isDragging = false;

            float threshold = _screenWidth * _swipeThreshold;

            if (_targetScreen >= 0 &&  Mathf.Abs(_dragDistance) >= threshold)
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
            if (_isAnimating || nextIndex < 0 || nextIndex >= _instructionScreens.Length)
                return;

            _isAnimating = true;

            var previousIndex = _currentScreen;

            var current = _screenTransforms[previousIndex];
            var next = _screenTransforms[nextIndex];

            var currentPosition = _screenPositions[previousIndex];
            var nextPosition = _screenPositions[nextIndex];

            var direction = nextIndex > previousIndex ? -1f : 1f;

            // Prepare incoming screen if this is button navigation
            if (_instructionScreens[nextIndex].screen.alpha == 0f)
                next.anchoredPosition = nextPosition + _screenWidth * -direction * Vector2.right;

            _instructionScreens[nextIndex].screen.alpha = 1f;
            _instructionScreens[nextIndex].screen.interactable = false;
            _instructionScreens[nextIndex].screen.blocksRaycasts = false;

            _instructionScreens[previousIndex].screen.interactable = false;
            _instructionScreens[previousIndex].screen.blocksRaycasts = false;

            _screenSequence = DOTween.Sequence();

            _screenSequence.Join(current.DOAnchorPos(currentPosition + _screenWidth * direction * Vector2.right, 
                _animationDuration));

            _screenSequence.Join(next.DOAnchorPos(nextPosition,_animationDuration));

            _screenSequence.SetEase(_animationEase);

            var hasUpdatedContent = false;

            _screenSequence.OnUpdate(() =>
            {
                float progress = _screenSequence.ElapsedPercentage(false);

                if (!hasUpdatedContent && progress >= 0.5f)
                {
                    hasUpdatedContent = true;

                    _currentScreen = nextIndex;

                    UpdateScreenContent();
                    UpdatePageDots();
                }
            });

            _screenSequence.OnComplete(() =>
            {
                _instructionScreens[previousIndex].screen.alpha = 0f;

                current.anchoredPosition = currentPosition;

                _currentScreen = nextIndex;

                _targetScreen = -1;

                _instructionScreens[nextIndex].screen.interactable = true;
                _instructionScreens[nextIndex].screen.blocksRaycasts = true;

                _isAnimating = false;
            });
        }

        private void SnapBack()
        {
            _isAnimating = true;

            var currentIndex = _currentScreen;

            var current = _screenTransforms[currentIndex];

            _screenSequence = DOTween.Sequence();

            _screenSequence.Join(current.DOAnchorPos(_screenPositions[currentIndex],_animationDuration));

            if (_targetScreen >= 0)
            {
                int nextIndex = _targetScreen;

                _screenSequence.Join(_screenTransforms[nextIndex].DOAnchorPos(_screenPositions[nextIndex] + Vector2.right * 
                    (_dragDistance < 0? _screenWidth: -_screenWidth),_animationDuration));
            }

            _screenSequence.SetEase(_animationEase);

            _screenSequence.OnComplete(() =>
            {
                if (_targetScreen >= 0)
                {
                    _instructionScreens[_targetScreen].screen.alpha = 0f;

                    _screenTransforms[_targetScreen].anchoredPosition = _screenPositions[_targetScreen];
                }

                UpdatePreviewContent(_currentScreen);
                _previewedScreen = _currentScreen;

                _targetScreen = -1;
                _isAnimating = false;
            });
        }

        #endregion


        #region Screen Content

        private void UpdateScreenContent()
        {
            var screenData = _instructionScreens[_currentScreen];

            _titleText.text = screenData.title;
            _description1Text.text = screenData.description1;
            _description2Text.text = screenData.description2;

            UpdateNavigationButtons();
        }

        private void UpdatePageDots()
        {
            for (int i = 0; i < _instructionScreens.Length; i++)
            {
                var pageDot = _instructionScreens[i].pageDot;

                if (pageDot == null)
                    continue;

                pageDot.sprite = i == _currentScreen ? _dotOnSprite : _dotOffSprite;
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

        public CanvasGroup screen;
        public Image pageDot;
    }
}
