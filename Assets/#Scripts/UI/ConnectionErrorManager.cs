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

        #region Unity Lifecycle

        private void Awake()
        {
            _retryButton.onClick.AddListener(RetryConnection);
        }

        private void OnDestroy()
        {
            _retryButton.onClick.RemoveListener(RetryConnection);
        }


        private void RetryConnection()
        {
            StartCoroutine(IE_RetryConnection());
        }

        private IEnumerator IE_RetryConnection()
        {
            _retryButton.interactable = false;

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

            _retryButton.interactable = true;
        }

        #endregion
    }
}