using TMPro;
using UnityEngine;

namespace UNO
{
    [RequireComponent(typeof(CanvasGroup))]
    public class NotificationView : MonoBehaviour
    {
        #region UI References

        [Header("UI References")]

        [SerializeField] private TextMeshProUGUI _notificationText1;
        [SerializeField] private TextMeshProUGUI _notificationText2;
        private CanvasGroup _canvasGroup;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            SetVisible(false);
        }

        public void Show(string text1, string text2, Color color)
        {
            if (_notificationText1 is not { } || _notificationText2 is not { })
                return;

            SetVisible(true);

            _notificationText1.SetText(text1);

            _notificationText2.color = color;
            _notificationText2.SetText(text2);
        }

        public void Hide()
        {
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
}