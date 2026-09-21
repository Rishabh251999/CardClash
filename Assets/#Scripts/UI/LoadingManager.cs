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

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_loadingSlider is { } slider)
                slider.value = 0f;
        }

        private void Start()
        {
            ConnectToServer();
        }

        private void Update()
        {
            if (_loadingSlider is not { } slider)
                return;

            UpdateFakeLoading(slider);

            if (_fakeLoadingComplete && NetworkClient.isConnected)
            {
                slider.value = ConnectedProgress;

                ResolveInitialScreen();

                enabled = false;
            }
        }

        #endregion

        #region Loading

        private void UpdateFakeLoading(Slider slider)
        {
            if (_fakeLoadingComplete)
                return;

            // Fake loading progresses independently.
            slider.value = Mathf.MoveTowards(
                slider.value,
                WaitingProgress,
                ProgressSpeed * Time.deltaTime
            );

            if (slider.value >= WaitingProgress)
            {
                slider.value = WaitingProgress;
                _fakeLoadingComplete = true;
            }
        }

        #endregion

        #region Client Methods

        private void ConnectToServer()
        {
            if (NetworkClient.isConnected || NetworkClient.active)
                return;

            if (NetworkManager.singleton is not { } networkManager)
                return;

            networkManager.StartClient();
        }

        private void ResolveInitialScreen()
        {
            var hasSavedUserName =
                !string.IsNullOrWhiteSpace(
                    PlayerPrefs.GetString("UserName", string.Empty)
                );

            if (_uiManager is { } uiManager)
            {
                uiManager.SetState(
                    hasSavedUserName
                        ? ScreenType.Lobby
                        : ScreenType.Login
                );
            }
        }

        #endregion
    }
}