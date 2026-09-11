using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UNO
{
    public class LoginManager : MonoBehaviour
    {
        #region UI References

        [Header("UI References")]

        [SerializeField] private Button _enterButton;
        [SerializeField] private TMP_InputField _userNameInputField;

        #endregion

        #region Runtime State

        private string _pendingUserName;

        #endregion


        #region Unity Lifecycle

        private void Awake()
        {
            _enterButton.onClick.AddListener(OnEnterButtonClicked);
            _userNameInputField.onValueChanged.AddListener(OnUserNameChanged);
        }

        private void OnDestroy()
        {
            _enterButton.onClick.RemoveListener(OnEnterButtonClicked);
            _userNameInputField.onValueChanged.RemoveListener(OnUserNameChanged);
        }

        private void OnUserNameChanged(string userName)
        {
            _enterButton.interactable = !string.IsNullOrWhiteSpace(userName);
        }

        private void OnEnterButtonClicked()
        {
            _pendingUserName = _userNameInputField.text;

            _enterButton.interactable = false;

            PlayerPrefs.SetString("UserName", _pendingUserName);

            NetworkManager.singleton.StartClient();
        }

        #endregion
    }
}
