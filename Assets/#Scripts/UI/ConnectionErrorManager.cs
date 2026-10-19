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
            _retryButton.onClick.AddListener(RetryConnection);

            if (_spinner is { })
                _spinnerRectTransform = _spinner.GetComponent<RectTransform>();
        }

        private void OnDestroy()
        {
            _retryButton.onClick.RemoveListener(RetryConnection);
        }


        private void RetryConnection()
        {
            StartCoroutine(IE_RetryConnection());
        }

        private void StartSpinner()
        {
            if (_spinner is not { })
            {
                return;
            }

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
                    Debug.Log("RetryConnection: Stale client state detected, stopping client before retry...");
                    NetworkManager.singleton.StopClient();
                }

                Debug.Log("RetryConnection: Attempting to reconnect to the server...");
                NetworkManager.singleton.StartClient();
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