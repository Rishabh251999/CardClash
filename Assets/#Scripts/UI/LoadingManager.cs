
using System;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class LoadingManager : MonoBehaviour
    {
        #region Constants

        private const float WaitingProgress = 0.3f;
        private const float ConnectedProgress = 1.0f;

        private const float FakeLoadingDuration = 3.0f;

        private const float HoldAtFullDuration = 0.5f;

        #endregion

        #region Script References

        [SerializeField] private UIManager _uiManager;

        #endregion

        #region UI References

        [Header("UI References")]
        [SerializeField] private Image _loadingSlider;

        #endregion

        #region Events

        /// <summary>
        /// Raised once the bar reaches full and holds before switching screens.
        /// </summary>
        public event Action OnLoadingComplete;

        #endregion

        #region Runtime State

        private bool _fakeLoadingComplete;
        private bool _connectionStarted;
        private bool _isLoadingState;
        private bool _reachedFull;
        private bool _completionRaised;
        private bool _startLoading;

        private float _fullHoldTimer;
        private float _fakeLoadingTimer;
        private float _connectingTimer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Application.isBatchMode)
                return;

            if (_uiManager != null)
            {
                _uiManager.OnStateChanged += HandleStateChange;
                _uiManager.OnScreenOpened += HandleScreenOpened;
            }

            if (_loadingSlider != null)
                _loadingSlider.fillAmount = 0f;
        }

        private void Update()
        {
            if (Application.isBatchMode || _loadingSlider == null || !_startLoading)
                return;

            if (!_fakeLoadingComplete)
            {
                UpdateFakeLoading(_loadingSlider);
                return;
            }

            if (!_isLoadingState)
                return;

            if (!_connectionStarted)
                ConnectToServer();

            if (_connectionStarted)
                UpdateConnectingProgress(_loadingSlider);
        }

        private void OnDestroy()
        {
            if (Application.isBatchMode || _uiManager == null)
                return;

            _uiManager.OnStateChanged -= HandleStateChange;
            _uiManager.OnScreenOpened -= HandleScreenOpened;
        }

        #endregion

        #region Loading Progress

        private void UpdateFakeLoading(Image slider)
        {
            if (_fakeLoadingComplete)
                return;

            _fakeLoadingTimer += Time.deltaTime;

            var progress = Mathf.Clamp01(_fakeLoadingTimer / FakeLoadingDuration);

            slider.fillAmount = Mathf.Lerp(0f, WaitingProgress, progress);

            if (progress >= 1f)
                _fakeLoadingComplete = true;
        }

        private void UpdateConnectingProgress(Image slider)
        {
            if (_reachedFull)
            {
                UpdateFullHold();
                return;
            }

            _connectingTimer += Time.deltaTime;

            var progress = Mathf.Clamp01(_connectingTimer / FakeLoadingDuration);

            slider.fillAmount = Mathf.Lerp(WaitingProgress,ConnectedProgress, progress);

            // Complete only when both conditions are satisfied.
            if (NetworkClient.isConnected && progress >= 1f)
            {
                slider.fillAmount = ConnectedProgress;

                _reachedFull = true;
                _fullHoldTimer = 0f;
            }
        }

        private void UpdateFullHold()
        {
            if (_completionRaised)
                return;

            _fullHoldTimer += Time.deltaTime;

            if (_fullHoldTimer >= HoldAtFullDuration)
            {
                _completionRaised = true;

                OnLoadingComplete?.Invoke();

                enabled = false;
            }
        }

        #endregion

        #region Connection

        private void ConnectToServer()
        {
            if (_connectionStarted || NetworkClient.isConnected || NetworkClient.active)
                return;

            _connectionStarted = true;
            NetworkManager.singleton.StartClient();
        }

        #endregion

        #region Screen Events

        private void HandleStateChange(ScreenType state)
        {
            _isLoadingState = state is ScreenType.Loading;

            if (!_isLoadingState)
                _startLoading = false;
        }

        private void HandleScreenOpened(ScreenType screenType)
        {
            if (screenType != ScreenType.Loading)
                return;

            _startLoading = true;

            _fakeLoadingComplete = false;
            _connectionStarted = false;
            _reachedFull = false;
            _completionRaised = false;

            _fakeLoadingTimer = 0f;
            _connectingTimer = 0f;
            _fullHoldTimer = 0f;

            if (_loadingSlider != null)
                _loadingSlider.fillAmount = 0f;
        }

        #endregion
    }
}
