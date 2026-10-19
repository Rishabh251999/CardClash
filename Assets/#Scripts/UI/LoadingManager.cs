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

        private const float HoldAtFullDuration = 0.5f; // how long to keep showing a full bar before finishing

        #endregion

        #region Script References

        [SerializeField] private UIManager _uiManager;

        #endregion

        #region UI References

        [Header("UI References")]
        [SerializeField] private Image _loadingSlider;

        #endregion

        #region Events

        /// <summary>Raised once the bar has visually reached full and held there, right before the screen switches.</summary>
        public event Action OnLoadingComplete;

        #endregion

        #region Runtime State

        private bool _fakeLoadingComplete;
        private bool _connectionStarted;
        private bool _isLoadingState;
        private bool _reachedFull;
        private bool _completionRaised;
        private float _fullHoldTimer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Application.isBatchMode)
                return;

            _uiManager.OnStateChanged += HandleStateChange;

            if (_loadingSlider is { } slider)
                slider.fillAmount = 0f;
        }

        private void Update()
        {
            if (Application.isBatchMode)
                return;

            if (_loadingSlider is not { } slider)
                return;

            // Phase 1: fake loading fills up to WaitingProgress independently of the connection.
            UpdateFakeLoading(slider);

            if (_fakeLoadingComplete && _isLoadingState)
                ConnectToServer();

            // Phase 2: once we've started connecting, keep animating the bar towards full
            // instead of leaving it stuck at WaitingProgress, so it never appears frozen.
            if (_connectionStarted)
                UpdateConnectingProgress(slider);
        }

        private void OnDestroy()
        {
            if (Application.isBatchMode)
                return;

            _uiManager.OnStateChanged -= HandleStateChange;
        }

        #endregion

        #region Progress Handling

        private void UpdateFakeLoading(Image slider)
        {
            if (_fakeLoadingComplete)
                return;

            // Fake loading progresses independently.
            slider.fillAmount = Mathf.MoveTowards(slider.fillAmount, WaitingProgress, ProgressSpeed * Time.deltaTime);

            if (slider.fillAmount >= WaitingProgress)
            {
                slider.fillAmount = WaitingProgress;
                _fakeLoadingComplete = true;
            }
        }

        private void UpdateConnectingProgress(Image slider)
        {
            slider.fillAmount = Mathf.MoveTowards(slider.fillAmount, ConnectedProgress, ProgressSpeed * Time.deltaTime);

            if (!_reachedFull)
            {
                // Mark the moment the bar visually reaches full while actually connected.
                if (NetworkClient.isConnected && slider.fillAmount >= ConnectedProgress)
                {
                    slider.fillAmount = ConnectedProgress;
                    _reachedFull = true;
                    _fullHoldTimer = 0f;
                }

                return;
            }

            if (_completionRaised)
                return;

            // Keep showing the completed bar for a short moment before switching screens.
            _fullHoldTimer += Time.deltaTime;

            if (_fullHoldTimer >= HoldAtFullDuration)
            {
                _completionRaised = true;

                OnLoadingComplete?.Invoke();

                enabled = false;
            }
        }

        private void ConnectToServer()
        {
            if (NetworkClient.isConnected || NetworkClient.active)
                return;

            Debug.Log("Connecting to server...");

            _connectionStarted = true;
            NetworkManager.singleton.StartClient();
        }

        private void HandleStateChange(ScreenType state)
        {
            _isLoadingState = state is ScreenType.Loading;
        }

        #endregion
    }
}