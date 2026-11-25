
using System;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class LoadingManager : MonoBehaviour
    {
        #region Constants

        private const float WaitingProgress = 0.3f;
        private const float ConnectionTimeout = 20f;
        private const float ConnectedProgress = 1.0f;
        private const float FakeLoadingDuration = 3.0f;
        private const float HoldAtFullDuration = 0.5f;
        private const float StagePauseDuration = 0.5f;

        // Loading duration based on ping (milliseconds).
        private const float MinValidPing = 0.001f;
        private const float FastConnectionDuration = 1.5f;
        private const float GoodConnectionDuration = 2.5f;
        private const float AverageConnectionDuration = 4.0f;
        private const float SlowConnectionDuration = 6.0f;
        private const float VerySlowConnectionDuration = 8.0f;

        private static readonly float[] StageThresholds =
        {
            0.15f,
            0.35f,
            0.60f,
            0.85f,
            1.00f
        };

        private static readonly string[] StageNames =
        {
            "PREPARING GAME...",
            "CONNECTING...",
            "SYNCING PLAYERS...",
            "LOADING ASSETS...",
            "FINALIZING MATCH..."
        };

        #endregion

        #region Script References

        [SerializeField] private UIManager _uiManager;
        [SerializeField] private NetworkConnectionChecker _networkConnectionChecker;

        #endregion

        #region UI References

        [Header("UI References")]
        [SerializeField] private Image _loadingSlider;
        [SerializeField] private TMP_Text _loadingStageText;

        #endregion

        #region Events

        public event Action OnLoadingComplete;

        #endregion

        #region Runtime State

        private bool _reachedFull;
        private bool _startLoading;
        private bool _isStagePaused;
        private bool _isLoadingState;
        private bool _completionRaised;
        private bool _connectionStarted;
        private bool _connectionTimedOut;
        private bool _fakeLoadingComplete;
        private bool _loadingDurationCalculated;

        private int _currentStageIndex;

        private float _fullHoldTimer;
        private float _connectingTimer;
        private float _loadingDuration;
        private float _stagePauseTimer;
        private float _displayProgress;
        private float _fakeLoadingTimer;
        private float _connectionTimeoutTimer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Application.isBatchMode) return;

            if (_uiManager != null)
            {
                _uiManager.OnStateChanged += HandleStateChange;
                _uiManager.OnScreenOpened += HandleScreenOpened;
            }

            if (_loadingSlider != null) _loadingSlider.fillAmount = 0f;

            SetLoadingStage(0);
        }

        private void Update()
        {
            if (Application.isBatchMode || _loadingSlider == null || !_startLoading || !_isLoadingState) return;
            if (_connectionTimedOut) return;

            if (!_fakeLoadingComplete)
            {
                UpdateFakeLoading(_loadingSlider);
                return;
            }

            if (!_connectionStarted) ConnectToServer();
            if (_connectionStarted) UpdateConnectingProgress(_loadingSlider);
        }

        private void OnDestroy()
        {
            if (Application.isBatchMode || _uiManager == null) return;

            _uiManager.OnStateChanged -= HandleStateChange;
            _uiManager.OnScreenOpened -= HandleScreenOpened;
        }

        #endregion

        #region Loading Stages

        private void SetLoadingStage(int index)
        {
            if (index < 0 || index >= StageNames.Length) return;

            _currentStageIndex = index;

            if (_loadingStageText != null) _loadingStageText.text = StageNames[index];
        }

        private float UpdateStagedProgress(float targetProgress)
        {
            if (_isStagePaused)
            {
                _stagePauseTimer += Time.deltaTime;

                if (_stagePauseTimer >= StagePauseDuration)
                {
                    _isStagePaused = false;
                    _stagePauseTimer = 0f;
                    SetLoadingStage(_currentStageIndex + 1);
                }

                return _displayProgress;
            }

            if (_currentStageIndex >= StageThresholds.Length) return _displayProgress;

            var stageBoundary = StageThresholds[_currentStageIndex];

            if (targetProgress >= stageBoundary && _displayProgress < stageBoundary)
            {
                _displayProgress = stageBoundary;

                if (_currentStageIndex < StageThresholds.Length - 1)
                {
                    _isStagePaused = true;
                    _stagePauseTimer = 0f;
                }

                return _displayProgress;
            }

            _displayProgress = Mathf.Max(_displayProgress, Mathf.Min(targetProgress, stageBoundary));

            return _displayProgress;
        }

        #endregion

        #region Loading Progress

        private float GetLoadingDuration()
        {
            var ping = (float)(NetworkTime.rtt * 1000f);

            if (ping < 50f) return FastConnectionDuration;
            if (ping < 100f) return GoodConnectionDuration;
            if (ping < 200f) return AverageConnectionDuration;
            if (ping < 300f) return SlowConnectionDuration;

            return VerySlowConnectionDuration;
        }

        private void UpdateFakeLoading(Image slider)
        {
            if (_fakeLoadingComplete) return;

            _fakeLoadingTimer += Time.deltaTime;

            var progress = Mathf.Clamp01(_fakeLoadingTimer / FakeLoadingDuration);
            var targetProgress = Mathf.Lerp(0f, WaitingProgress, progress);

            slider.fillAmount = UpdateStagedProgress(targetProgress);

            if (progress >= 1f) _fakeLoadingComplete = true;
        }

        private void UpdateConnectingProgress(Image slider)
        {
            if (!NetworkClient.isConnected)
            {
                _connectionTimeoutTimer += Time.deltaTime;
                slider.fillAmount = UpdateStagedProgress(WaitingProgress);

                if (_connectionTimeoutTimer >= ConnectionTimeout && !_connectionTimedOut)
                {
                    _connectionTimedOut = true;
                    Debug.LogError("Server connection timed out.");

                    CardNetworkManager.Singleton.HandleConnectionTimeout();
                }

                return;
            }

            if (!_loadingDurationCalculated)
            {
                var ping = (float)NetworkTime.rtt;

                if (ping < MinValidPing)
                {
                    slider.fillAmount = UpdateStagedProgress(WaitingProgress);
                    return;
                }

                _loadingDuration = GetLoadingDuration();
                _loadingDurationCalculated = true;
            }

            if (_reachedFull)
            {
                UpdateFullHold();
                return;
            }

            _connectingTimer += Time.deltaTime;

            var progress = Mathf.Clamp01(_connectingTimer / _loadingDuration);
            var targetProgress = Mathf.Lerp(WaitingProgress, ConnectedProgress, progress);

            slider.fillAmount = UpdateStagedProgress(targetProgress);

            if (_displayProgress >= ConnectedProgress)
            {
                slider.fillAmount = ConnectedProgress;
                _reachedFull = true;
                _fullHoldTimer = 0f;
            }
        }

        private void UpdateFullHold()
        {
            if (_completionRaised) return;

            _fullHoldTimer += Time.deltaTime;

            if (_fullHoldTimer >= HoldAtFullDuration)
            {
                _completionRaised = true;
                _startLoading = false;
                OnLoadingComplete?.Invoke();
            }
        }

        #endregion

        #region Connection

        private void ConnectToServer()
        {
            if (_connectionStarted || NetworkClient.isConnected || NetworkClient.active) return;

            if (NetworkManager.singleton == null)
            {
                Debug.LogError("NetworkManager singleton not found.");
                _connectionTimedOut = true;
                return;
            }

            if (!_networkConnectionChecker.CheckInitialConnection())
            {
                _connectionTimedOut = true; // Prevents Update() from retrying every frame.
                return;
            }

            _connectionStarted = true;
            NetworkManager.singleton.StartClient();
        }

        #endregion

        #region Screen Events

        private void HandleStateChange(ScreenType state)
        {
            _isLoadingState = state == ScreenType.Loading;

            if (!_isLoadingState) _startLoading = false;
        }

        private void HandleScreenOpened(ScreenType screenType)
        {
            if (screenType != ScreenType.Loading) return;

            _isLoadingState = true;
            _startLoading = true;

            // Reset connection state.
            _reachedFull = false;
            _completionRaised = false;
            _connectionStarted = false;
            _connectionTimedOut = false;
            _fakeLoadingComplete = false;
            _loadingDurationCalculated = false;

            // Reset timers.
            _fullHoldTimer = 0f;
            _connectingTimer = 0f;
            _loadingDuration = 0f;
            _fakeLoadingTimer = 0f;
            _connectionTimeoutTimer = 0f;

            // Reset stages.
            _currentStageIndex = 0;
            _stagePauseTimer = 0f;
            _displayProgress = 0f;
            _isStagePaused = false;

            SetLoadingStage(0);

            if (_loadingSlider != null) _loadingSlider.fillAmount = 0f;
        }

        #endregion
    }
}
