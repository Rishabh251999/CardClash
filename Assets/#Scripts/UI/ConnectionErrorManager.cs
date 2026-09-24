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

        [Space(2.5f)]

        [SerializeField] private CanvasGroup _reconnecting;

        #endregion

        #region Scripts References

        [SerializeField] private Animation _reconnectingAnimation;

        #endregion

        #region Runtime State

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _retryButton.onClick.AddListener(RetryConnection);

            _reconnecting.alpha = 0.0f;
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
            _reconnecting.alpha = 1.0f;

            _reconnectingAnimation.Play();

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
            _reconnectingAnimation.Stop();

            _reconnecting.alpha = 0.0f;
        }

        #endregion
    }
}