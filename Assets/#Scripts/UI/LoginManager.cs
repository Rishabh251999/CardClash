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
            _enterButton.onClick.AddListener(OnEnterButtonClicked);
            _userNameInputField.onValueChanged.AddListener(OnUserNameChanged);

            _userNameInputField.characterLimit = MaxUserNameLength;
        }

        private void OnDestroy()
        {
            _enterButton.onClick.RemoveListener(OnEnterButtonClicked);
            _userNameInputField.onValueChanged.RemoveListener(OnUserNameChanged);
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

            PlayerPrefs.SetString("UserName", _pendingUserName);

            if (_uiManager is { })
                _uiManager.SetState(ScreenType.Lobby);
        }

        #endregion
    }
}