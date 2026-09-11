using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardClash
{
    public class UIManager : MonoBehaviour
    {
        #region UI References

        [SerializeField] private List<PanelEntry> _panels;

        #endregion

        #region Runtime State

        public ScreenType ScreenType { get; private set; }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            var hasSavedUserName = !string.IsNullOrWhiteSpace(PlayerPrefs.GetString("UserName", string.Empty));

            if (hasSavedUserName)
                NetworkManager.singleton.StartClient();

            else
                SetState(ScreenType.Login);
        }

        public void SetState(ScreenType state)
        {
            ScreenType = state;

            foreach (var entry in _panels)
                SetVisible(entry.CanvasGroup, entry.ScreenType == state);
        }

        private void SetVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        #endregion
    }
}