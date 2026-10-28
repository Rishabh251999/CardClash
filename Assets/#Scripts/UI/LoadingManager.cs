
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
        private const float ProgressSpeed = 1.0f / FakeLoadingDuration;

        private const float HoldAtFullDuration = 0.5f;

        // Fewer steps with smooth transitions between them.
        private const int FakeLoadingSteps = 2;
        private const int ConnectingSteps = 5;

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
            if (Application.isBatchMode ||
                _loadingSlider == null ||
                !_startLoading)
            {
                return;
            }

            UpdateFakeLoading(_loadingSlider);

            if (_fakeLoadingComplete && _isLoadingState)
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

            float normalizedProgress = Mathf.Clamp01(
                _fakeLoadingTimer / FakeLoadingDuration
            );

            float stepSize = WaitingProgress / FakeLoadingSteps;

            int currentStep = Mathf.Min(
                Mathf.FloorToInt(normalizedProgress * FakeLoadingSteps),
                FakeLoadingSteps
            );

            float targetProgress = normalizedProgress >= 1f
                ? WaitingProgress
                : currentStep * stepSize;

            // Smoothly animate toward each step.
            slider.fillAmount = Mathf.MoveTowards(
                slider.fillAmount,
                targetProgress,
                ProgressSpeed * Time.deltaTime
            );

            if (normalizedProgress >= 1f &&
                slider.fillAmount >= WaitingProgress - 0.001f)
            {
                slider.fillAmount = WaitingProgress;
                _fakeLoadingComplete = true;
            }
        }

        private void UpdateConnectingProgress(Image slider)
        {
            if (_reachedFull)
            {
                UpdateFullHold();
                return;
            }

            _connectingTimer += Time.deltaTime;

            float normalizedProgress = Mathf.Clamp01(
                _connectingTimer / FakeLoadingDuration
            );

            float remainingProgress = ConnectedProgress - WaitingProgress;
            float stepSize = remainingProgress / ConnectingSteps;

            int currentStep = Mathf.Min(
                Mathf.FloorToInt(normalizedProgress * ConnectingSteps),
                ConnectingSteps
            );

            float targetProgress = normalizedProgress >= 1f
                ? ConnectedProgress
                : WaitingProgress + currentStep * stepSize;

            // Smoothly animate between step targets.
            slider.fillAmount = Mathf.MoveTowards(
                slider.fillAmount,
                targetProgress,
                ProgressSpeed * Time.deltaTime
            );

            // Finish only after the client is actually connected.
            if (NetworkClient.isConnected &&
                normalizedProgress >= 1f &&
                slider.fillAmount >= ConnectedProgress - 0.001f)
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
            if (_connectionStarted ||
                NetworkClient.isConnected ||
                NetworkClient.active)
            {
                return;
            }

            Debug.Log("Connecting to server...");

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
            if (screenType is ScreenType.Loading)
                _startLoading = true;
        }

        #endregion
    }
}
