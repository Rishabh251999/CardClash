using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class ProfileManager : MonoBehaviour
    {
        #region Script References

        [SerializeField] private UIManager _uiManager;

        #endregion

        #region UI References

        [Header("UI References")]
        [SerializeField] private TMP_InputField _usernameInputField;

        [Space(2.5f)]
        [SerializeField] private Button _backButton;

        #endregion

        #region Runtime State

        private bool _isEditing;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _backButton.onClick.AddListener(OnBackButtonClick);
            _uiManager.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            _backButton.onClick.RemoveListener(OnBackButtonClick);
            _uiManager.OnStateChanged -= OnStateChanged;
        }

        private void OnBackButtonClick()
        {
            _uiManager.SetState(ScreenType.Lobby, true);
        }

        private void OnStateChanged(ScreenType state)
        {
            if (state == ScreenType.Profile)
            {
                var username = PlayerPrefs.GetString("UserName", string.Empty);

                _usernameInputField.SetTextWithoutNotify(username);
            }
        }

        #endregion
    }
}