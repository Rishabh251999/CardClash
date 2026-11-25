using Mirror;
using System;
using System.Collections;
using TMPro;
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
        [Space(2.5f)]
        [SerializeField] private Image _errorIconImage;
        [Space(2.5f)]
        [SerializeField] private TextMeshProUGUI _errorTitleText;
        [SerializeField] private TextMeshProUGUI _errorMessage1Text;
        [SerializeField] private TextMeshProUGUI _errorMessage2Text;

        #endregion

        #region Runtime Collections

        [SerializeField] private ConnectionErrorData[] _connectionErrorData;

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

        private void Awake()
        {
            _errorIconImage.sprite = null;
            _errorIconImage.color = new(1, 1, 1, 0);
        }

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

        public void ShowError(ConnectionErrorType connectionErrorType)
        {
            var errorData = Array.Find(_connectionErrorData, 
                data => data.ConnectionErrorType == connectionErrorType);

            SetError(errorData.Icon, errorData.Title, errorData.Message1, errorData.Message2);
        }

        private void SetError(Sprite icon, string title, string message1, string message2)
        {
            if (_errorIconImage.color.a == 0f)
                _errorIconImage.color = new(1, 1, 1, 1);

            _errorIconImage.sprite = icon;

            _errorTitleText.text = title;
            _errorMessage1Text.text = message1;
            _errorMessage2Text.text = message2;
        }

        private IEnumerator IE_RetryConnection()
        {
            _retryButton.interactable = false;

            StartSpinner();

            if (!NetworkServer.active)
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

    [Serializable]
    public struct ConnectionErrorData
    {
        public ConnectionErrorType ConnectionErrorType;

        public Sprite Icon;

        public string Title;
        public string Message1;
        public string Message2;
    }

}