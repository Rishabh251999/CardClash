using System;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class GameButtonManager : MonoBehaviour
    {
        #region Script References

        [SerializeField] private ButtonSprites _passButtonSprites;
        [SerializeField] private ButtonSprites _calloutButtonSprites;

        #endregion

        #region UI References

        [Header("UI References")]
        [SerializeField] private Button _passButton;
        [SerializeField] private Button _calloutButton;
        [Space(2.5f)]
        [SerializeField] private Image _passButtonIcon;
        [SerializeField] private Image _calloutButtonIcon;

        #endregion

        #region Runtime State

        private Image _passButtonImage;
        private Image _calloutButtonImage;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _passButtonImage = _passButton.GetComponent<Image>();
            _calloutButtonImage = _calloutButton.GetComponent<Image>();
        }

        public void SetPassButtonInteractable(bool isInteractable)
        {
            _passButton.interactable = isInteractable;

            _passButtonImage.sprite = isInteractable ? _passButtonSprites.EnabledSprite : _passButtonSprites.DisabledSprite;

            _passButtonIcon.sprite = isInteractable ? _passButtonSprites.EnabledIconSprite : _passButtonSprites.DisabledIconSprite;
        }

        public void SetCallOutButtonInteractable(bool isInteractable)
        {
            _calloutButton.interactable = isInteractable;

            _calloutButtonImage.sprite = isInteractable ? _calloutButtonSprites.EnabledSprite : _calloutButtonSprites.DisabledSprite;

            _calloutButtonIcon.sprite = isInteractable ? _calloutButtonSprites.EnabledIconSprite : _calloutButtonSprites.DisabledIconSprite;
        }

        #endregion
    }

    [Serializable]
    public record ButtonSprites
    {
        public Sprite EnabledSprite;
        public Sprite DisabledSprite;
        public Sprite EnabledIconSprite;
        public Sprite DisabledIconSprite;
    }
}
