using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    [RequireComponent(typeof(CanvasGroup))]
    public class NotificationView : MonoBehaviour
    {
        #region UI References

        [Header("UI References")]
        [SerializeField] private Image _icon;
        [SerializeField] private Image _background;
        [Space(2.5f)]
        [SerializeField] private TextMeshProUGUI _notificationText;
        private CanvasGroup _canvasGroup;

        #endregion

        #region Inspector - Notification States

        [Space(5f)]
        [Header("Notification States")]
        [SerializeField] private NotificationState _redState;
        [SerializeField] private NotificationState _blueState;
        [SerializeField] private NotificationState _greenState;
        [SerializeField] private NotificationState _yellowState;
        [SerializeField] private NotificationState _lastCardState;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            SetVisible(false);
        }

        public void Show(NotificationType type, string text)
        {
            var state = type switch
            {
                NotificationType.Red => _redState,
                NotificationType.Blue => _blueState,
                NotificationType.Green => _greenState,
                NotificationType.Yellow => _yellowState,
                NotificationType.LastCard => _lastCardState,
                _ => null
            };

            Show(state, text);
        }

        private void Show(NotificationState state, string text)
        {
            if (_notificationText is not { } || _icon is not { } || _background is not { })
                return;

            SetVisible(true);

            _icon.sprite = state.Icon;
            _icon.enabled = state.Icon is { };

            _background.sprite = state.background;

            _notificationText.SetText(text);
        }

        public void Hide()
        {
            _icon.sprite = null;
            _background.sprite = null;

            _notificationText.SetText(string.Empty);

            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        #endregion
    }

    [Serializable]
    public record NotificationState
    {
        public NotificationType Type;
        public Sprite background;
        public Sprite Icon;
    }
}