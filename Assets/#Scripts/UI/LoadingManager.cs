using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class LoadingManager : MonoBehaviour
    {
        #region Constants

        private const float WaitingProgress = 0.9f;
        private const float ConnectedProgress = 1.0f;

        private const float FakeLoadingDuration = 3.0f;
        private const float ProgressSpeed = 1.0f / FakeLoadingDuration;

        #endregion

        #region Script References

        [SerializeField] private UIManager _uiManager;

        #endregion

        #region UI References

        [Header("UI References")]
        [SerializeField] private Slider _loadingSlider;

        #endregion

        #region Runtime State

        private bool _fakeLoadingComplete;
        private bool _isLoadingState;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Application.isBatchMode)
                return;

            _uiManager.OnStateChanged += HandleStateChange;

            if (_loadingSlider is { } slider)
                slider.value = 0f;
        }

        private void Update()
        {
            if (Application.isBatchMode)
                return;

            if (_loadingSlider is not { } slider)
                return;

            UpdateFakeLoading(slider);

            if (_fakeLoadingComplete && _isLoadingState)
                ConnectToServer();

            if (_fakeLoadingComplete && NetworkClient.isConnected)
            {
                slider.value = ConnectedProgress;

                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (Application.isBatchMode)
                return;

            _uiManager.OnStateChanged -= HandleStateChange;
        }

        private void UpdateFakeLoading(Slider slider)
        {
            if (_fakeLoadingComplete)
                return;

            // Fake loading progresses independently.
            slider.value = Mathf.MoveTowards(slider.value, WaitingProgress,ProgressSpeed * Time.deltaTime);

            if (slider.value >= WaitingProgress)
            {
                slider.value = WaitingProgress;
                _fakeLoadingComplete = true;
            }
        }

        private void ConnectToServer()
        {
            if (NetworkClient.isConnected || NetworkClient.active)
                return;

            Debug.Log("Connecting to server...");

            NetworkManager.singleton.StartClient();
        }

        private void HandleStateChange(ScreenType state)
        {
            _isLoadingState = state is ScreenType.Loading;
        }

        #endregion
    }
}