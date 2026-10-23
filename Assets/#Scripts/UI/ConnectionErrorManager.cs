using Mirror;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class ConnectionErrorManager : MonoBehaviour
    {
        #region Constants/Readonly 

        private readonly WaitForSeconds _waitForSeconds5_0 = new(5.0f);

        #endregion

        #region Script References

        [SerializeField] private UIManager _uiManager;

        #endregion

        #region UI References

        [Header("UI References")]
        [SerializeField] private Button _retryButton;

        #endregion

        #region Gameobjects

        [SerializeField] private GameObject _spinner;

        #endregion

        #region Settings

        [Header("Spinner Settings")]
        [SerializeField] private float _spinnerRotationSpeed = 200.0f;

        #endregion

        #region Runtime State

        private RectTransform _spinnerRectTransform;
        private Coroutine _spinnerCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _retryButton.AddListenerSafe(RetryConnection);

            if (_spinner is { })
                _spinnerRectTransform = _spinner.GetComponent<RectTransform>();
        }

        private void OnDestroy()
        {
            _retryButton.RemoveListenerSafe(RetryConnection);
        }


        private void RetryConnection()
        {
            StartCoroutine(IE_RetryConnection());
        }

        private void StartSpinner()
        {
            if (_spinner is not { })
                return;

            _spinnerCoroutine ??= StartCoroutine(IE_RotateSpinner());
        }

        private void StopSpinner()
        {
            if (_spinnerCoroutine is { })
            {
                StopCoroutine(_spinnerCoroutine);
                _spinnerCoroutine = null;
            }
        }

        private IEnumerator IE_RetryConnection()
        {
            _retryButton.interactable = false;

            StartSpinner();

            if (NetworkServer.active)
            {
                Debug.LogWarning("RetryConnection: NetworkServer is active, cannot retry connection.");
            }
            else
            {
                if (NetworkClient.isConnected || NetworkClient.active)
                {
                    NetworkManager.singleton.StopClient();
                }

                NetworkManager.singleton.StartClient();

                float elapsed = 0f;

                while (elapsed < 5.0f && !NetworkClient.isConnected)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (NetworkClient.isConnected)
                {
                    StopSpinner();

                    _retryButton.interactable = true;

                    // Client reconnected successfully - skip the loading screen entirely and go straight to the lobby.
                    _uiManager.SetState(ScreenType.Lobby, false);

                    yield break;
                }
            }

            yield return _waitForSeconds5_0;

            StopSpinner();

            _retryButton.interactable = true;
        }

        private IEnumerator IE_RotateSpinner()
        {
            while (true)
            {
                _spinnerRectTransform.Rotate(0f, 0f, -_spinnerRotationSpeed * Time.deltaTime);
                yield return null;
            }
        }

        #endregion
    }
}