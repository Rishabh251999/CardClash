using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace CardClash
{
    internal static class ButtonExtensions
    {
        #region Button

        internal static void AddListenerSafe(this Button button, UnityAction action)
        {
            if (button is not { })
                return;

            button.onClick.AddListener(() => AudioManager.Instance.PlaySoundEffect("ui_click"));
            button.onClick.AddListener(action);
        }

        internal static void RemoveListenerSafe(this Button button, UnityAction action)
        {
            if (button is not { })
                return;

            button.onClick.RemoveListener(() => AudioManager.Instance.PlaySoundEffect("ui_click"));
            button.onClick.RemoveListener(action);
        }

        #endregion

        #region TMP_InputField

        internal static void AddListenerSafe(this TMP_InputField inputField, UnityAction<string> action)
        {
            if (inputField is { })
                inputField.onValueChanged.AddListener(action);
        }

        internal static void RemoveListenerSafe(this TMP_InputField inputField, UnityAction<string> action)
        {
            if (inputField is { })
                inputField.onValueChanged.RemoveListener(action);
        }

        #endregion

        #region TMP_Dropdown

        internal static void AddListenerSafe(this TMP_Dropdown dropdown, UnityAction<int> action)
        {
            if (dropdown is { })
                dropdown.onValueChanged.AddListener(action);
        }

        internal static void RemoveListenerSafe(this TMP_Dropdown dropdown, UnityAction<int> action)
        {
            if (dropdown is { })
                dropdown.onValueChanged.RemoveListener(action);
        }

        #endregion

        #region Toggle

        public static void AddListenerSafe(this Toggle toggle, UnityAction<bool> action)
        {
            if (toggle is { })
                toggle.onValueChanged.AddListener(action);
        }

        public static void RemoveListenerSafe(this Toggle toggle, UnityAction<bool> action)
        {
            if (toggle is { })
                toggle.onValueChanged.RemoveListener(action);
        }

        #endregion
    }
}
