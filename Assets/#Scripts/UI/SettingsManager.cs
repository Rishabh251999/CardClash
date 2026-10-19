
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class ProfileManager : MonoBehaviour
    {
        #region Constants

        private const int MinUserNameLength = 3;
        private const int MaxUserNameLength = 16;

        private const string UserNameKey = "UserName";
        private const string SoundKey = "Sound";
        private const string VibrationKey = "Vibration";

        private const string OnLabel = "ON";
        private const string OffLabel = "OFF";

        private readonly Color32 OnColor = new(0xC3, 0xA8, 0xFF, 255);

        private readonly Color32 OffColor = new(0x6B, 0x70, 0x96, 255);

        #endregion

        #region Scripts References

        [Header("References")]
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private AudioManager _audioManager;

        [SerializeField] private SettingsData _soundToggle;
        [SerializeField] private SettingsData _vibrationToggle;

        #endregion

        #region UI References

        [Header("UI References")]
        [Space(5f)]
        [SerializeField] private TMP_InputField _usernameInputField;
        [Space(2.5f)]
        [SerializeField] private Button _editButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _saveButton;

        #endregion

        #region Sprites

        [Header("Sprites")]
        [Space(5f)]
        [SerializeField] private Sprite _onSprite;
        [SerializeField] private Sprite _offSprite;

        #endregion

        #region Runtime Settings 

        [Header("Runtime Settings")]
        [Space(5f)]

        [SerializeField, Min(0f)]
        private float _toggleAnimDuration = 0.12f;

        [SerializeField, Range(0.1f, 1f)]
        private float _toggleSquashScale = 0.85f;

        #endregion

        #region Runtime State

        private string _originalUsername;

        private bool _originalSoundEnabled;
        private bool _originalVibrationEnabled;

        private Coroutine _soundAnimation;
        private Coroutine _vibrationAnimation;

        private Vector3 _soundOriginalScale;
        private Vector3 _vibrationOriginalScale;

        private bool HasUnsavedChanges => _usernameInputField.text != _originalUsername || _soundToggle.toggle.isOn != _originalSoundEnabled ||
            _vibrationToggle.toggle.isOn != _originalVibrationEnabled;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _soundOriginalScale = _soundToggle.toggle.image.rectTransform.localScale;

            _vibrationOriginalScale = _vibrationToggle.toggle.image.rectTransform.localScale;
        }

        private void Start()
        {
            _backButton.onClick.AddListener(OnBackButtonClick);
            _saveButton.onClick.AddListener(OnSaveButtonClick);
            _editButton.onClick.AddListener(OnEditButtonClick);

            _usernameInputField.onValueChanged.AddListener(OnUsernameValueChanged);

            _soundToggle.toggle.onValueChanged.AddListener(OnSoundToggleChanged);
            _vibrationToggle.toggle.onValueChanged.AddListener(OnVibrationToggleChanged);

            _uiManager.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (_backButton != null)
                _backButton.onClick.RemoveListener(OnBackButtonClick);

            if (_saveButton != null)
                _saveButton.onClick.RemoveListener(OnSaveButtonClick);

            if (_editButton != null)
                _editButton.onClick.RemoveListener(OnEditButtonClick);

            if (_usernameInputField != null)
                _usernameInputField.onValueChanged.RemoveListener(OnUsernameValueChanged);

            if (_soundToggle?.toggle != null)
                _soundToggle.toggle.onValueChanged.RemoveListener(OnSoundToggleChanged);

            if (_vibrationToggle?.toggle != null)
                _vibrationToggle.toggle.onValueChanged.RemoveListener(OnVibrationToggleChanged);

            if (_uiManager != null)
                _uiManager.OnStateChanged -= OnStateChanged;

            StopToggleAnimation(_soundToggle, ref _soundAnimation, _soundOriginalScale);

            StopToggleAnimation(_vibrationToggle, ref _vibrationAnimation, _vibrationOriginalScale);
        }

        private void OnStateChanged(ScreenType state)
        {
            if (state != ScreenType.Settings)
                return;

            LoadSettings();
        }

        private void LoadSettings()
        {
            var username =  PlayerPrefs.GetString(UserNameKey, string.Empty);

            var soundEnabled = PlayerPrefs.GetInt(SoundKey, 1) == 1;
            var vibrationEnabled = PlayerPrefs.GetInt(VibrationKey, 1) == 1;

            _usernameInputField.SetTextWithoutNotify(username);
            _usernameInputField.interactable = false;

            _soundToggle.toggle.SetIsOnWithoutNotify(soundEnabled);
            _vibrationToggle.toggle.SetIsOnWithoutNotify(vibrationEnabled);

            SetToggleVisualInstant(_soundToggle);
            SetToggleVisualInstant(_vibrationToggle);

            CacheOriginalValues();

            RefreshSaveButton();
        }

        private void CacheOriginalValues()
        {
            _originalUsername = _usernameInputField.text;
            _originalSoundEnabled = _soundToggle.toggle.isOn;
            _originalVibrationEnabled = _vibrationToggle.toggle.isOn;
        }

        #endregion

        #region Toggle Visuals

        private void SetToggleVisualInstant(SettingsData data)
        {
            data.toggle.image.sprite = data.toggle.isOn ? _onSprite : _offSprite;

            UpdateStatusText(data);
        }

        private void UpdateStatusText(SettingsData data)
        {
            bool isOn = data.toggle.isOn;

            data.statusText.text = isOn ? OnLabel : OffLabel;
            data.statusText.color = isOn ? OnColor : OffColor;
        }

        private void OnSoundToggleChanged(bool isOn)
        {
            HandleToggleChanged(_soundToggle, ref _soundAnimation, _soundOriginalScale);
        }

        private void OnVibrationToggleChanged(bool isOn)
        {
            HandleToggleChanged(_vibrationToggle, ref _vibrationAnimation, _vibrationOriginalScale);
        }

        private void HandleToggleChanged(SettingsData data, ref Coroutine animation, Vector3 originalScale)
        {
            UpdateStatusText(data);

            StopToggleAnimation(data, ref animation, originalScale);

            if (_toggleAnimDuration <= 0f)
            {
                SetToggleVisualInstant(data);
            }
            else
            {
                animation = StartCoroutine(AnimateToggle(data, originalScale));
            }

            RefreshSaveButton();
        }

        private void StopToggleAnimation(SettingsData data, ref Coroutine animation, Vector3 originalScale)
        {
            if (animation != null)
            {
                StopCoroutine(animation);
                animation = null;
            }

            if (data.toggle.image != null)
            {
                data.toggle.image.rectTransform.localScale =
                    originalScale;
            }
        }

        private IEnumerator AnimateToggle(SettingsData data, Vector3 originalScale)
        {
            var rect = data.toggle.image.rectTransform;

            var squashedScale = originalScale * _toggleSquashScale;

            var halfDuration = _toggleAnimDuration * 0.5f;
            var elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;

                rect.localScale = Vector3.Lerp(originalScale, squashedScale, Mathf.Clamp01(elapsed / halfDuration));

                yield return null;
            }

            rect.localScale = squashedScale;

            data.toggle.image.sprite = data.toggle.isOn ? _onSprite : _offSprite;

            elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;

                rect.localScale = Vector3.Lerp(squashedScale, originalScale, Mathf.Clamp01(elapsed / halfDuration));

                yield return null;
            }

            rect.localScale = originalScale;

            if (data == _soundToggle)
                _soundAnimation = null;
            else
                _vibrationAnimation = null;
        }

        private void OnUsernameValueChanged(string username)
        {
            RefreshSaveButton();
        }

        private void RefreshSaveButton()
        {
            _saveButton.interactable = HasUnsavedChanges;
        }

        #endregion

        #region Save / Navigation

        private void OnBackButtonClick()
        {
            _uiManager.SetState(ScreenType.Lobby, true);
        }

        private void OnSaveButtonClick()
        {
            if (!HasUnsavedChanges)
                return;

            _audioManager.SetMusicEnabled(_soundToggle.toggle.isOn);

            var username = _usernameInputField.text.Trim();

            if (username.Length < MinUserNameLength || username.Length > MaxUserNameLength)
                return;

            _usernameInputField.SetTextWithoutNotify(username);
            _usernameInputField.interactable = false;

            PlayerPrefs.SetString(UserNameKey, username);

            PlayerPrefs.SetInt(SoundKey,_soundToggle.toggle.isOn ? 1 : 0);
            PlayerPrefs.SetInt(VibrationKey, _vibrationToggle.toggle.isOn ? 1 : 0);

            PlayerPrefs.Save();

            CacheOriginalValues();
            RefreshSaveButton();
        }

        private void OnEditButtonClick()
        {
            _usernameInputField.interactable = !_usernameInputField.interactable;

            if (_usernameInputField.interactable)
            {
                _usernameInputField.Select();
                _usernameInputField.caretPosition = _usernameInputField.text.Length;
            }
        }

        #endregion
    }

    [Serializable]
    public class SettingsData
    {
        public TextMeshProUGUI statusText;
        public Toggle toggle;
    }
}
