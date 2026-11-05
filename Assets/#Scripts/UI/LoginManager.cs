using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class LoginManager : MonoBehaviour
    {
        #region Constants/Readonly 
        private const int MinUserNameLength = 3;
        private const int MaxUserNameLength = 16;

        #endregion

        #region Script References

        [SerializeField] private UIManager _uiManager;
        [SerializeField] private SettingsSO _settings;

        #endregion

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
            _enterButton.AddListenerSafe(OnEnterButtonClicked);
            _userNameInputField.AddListenerSafe(OnUserNameChanged);

            _userNameInputField.characterLimit = MaxUserNameLength;
        }

        private void OnDestroy()
        {
            _enterButton.RemoveListenerSafe(OnEnterButtonClicked);
            _userNameInputField.AddListenerSafe(OnUserNameChanged);
        }

        private void OnUserNameChanged(string userName)
        {
            var isValidLength = userName.Length >= MinUserNameLength && userName.Length <= MaxUserNameLength;

            _enterButton.interactable = isValidLength && !string.IsNullOrWhiteSpace(userName);
        }

        private void OnEnterButtonClicked()
        {
            _pendingUserName = _userNameInputField.text;

            _enterButton.interactable = false;

            _settings.UserName = _pendingUserName;
            _settings.Save();

            _uiManager.SetState(ScreenType.Loading, false);
        }

        //private void TryConnect()
        //{
        //    if (NetworkServer.active || NetworkClient.isConnected || NetworkClient.active)
        //        return;

        //    NetworkManager.singleton.StartClient();
        //}

        #endregion
    }
}