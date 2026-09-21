using Mirror;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class ConnectionErrorManager : MonoBehaviour
    {
        #region Constants/Readonly 

        private const float RetryDuration = 5.0f;
        private readonly WaitForSeconds _waitForSeconds1_0 = new(1.0f);

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

        private Coroutine _retryCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _retryButton.onClick.AddListener(RetryConnection);

            _reconnecting.alpha = 0.0f;

            SetReconnectingAnimation(false);
        }

        private void OnDestroy()
        {
            _retryButton.onClick.RemoveListener(RetryConnection);
        }


        private void RetryConnection()
        {
            if (_retryCoroutine != null)
                return;

            _retryCoroutine = StartCoroutine(RetryConnectionRoutine());
        }

        private IEnumerator RetryConnectionRoutine()
        {
            _retryButton.interactable = false;
            _reconnecting.alpha = 1.0f;

            SetReconnectingAnimation(true);

            if (NetworkServer.active)
            {
                yield return _waitForSeconds1_0;
            }

            var elapsedTime = 0.0f;

            NetworkManager.singleton.StartClient();

            while (elapsedTime < RetryDuration)
            {
                if (NetworkClient.isConnected)
                {
                    break;
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            SetReconnectingAnimation(false);

            _reconnecting.alpha = 0.0f;
            _retryButton.interactable = true;

            _retryCoroutine = null;
        }

        private void SetReconnectingAnimation(bool isPlaying)
        {
            if (_reconnectingAnimation == null)
                return;

            if (isPlaying)
                _reconnectingAnimation.Play();
            else
                _reconnectingAnimation.Stop();
        }

        #endregion
    }
}