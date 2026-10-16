using Mirror;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class PlayerGUI : MonoBehaviour
    {
        private const string AvatarsResourcePath = "Avatars/";
        private const int WaitingAvatarIndex = 0;

        [SerializeField] private PlayerGUISprites _playerGUISprites;


        [SerializeField] private Image _readyImage;
        [SerializeField] private Image _avatarImage;

        [SerializeField] private TextMeshProUGUI _playerNameText;

        [SerializeField] private GameObject _roomOwnerGameObject;

        [ClientCallback]
        public void SetPlayerInfo(PlayerRoomInfo info)
        {
            _playerNameText.SetText($"{info.playerName}");

            _roomOwnerGameObject.SetActive(info.isOwner);

            if (_readyImage != null)
                _readyImage.sprite = info.isReady ? _playerGUISprites.readySprite : _playerGUISprites.notReadySprite;

            SetAvatar(info.avatarId);
        }

        [ClientCallback]
        public void SetWaiting()
        {
            _playerNameText.SetText("Waiting...");

            _roomOwnerGameObject.SetActive(false);

            if (_readyImage != null)
                _readyImage.sprite = _playerGUISprites.waitingSprite;

            SetAvatar(WaitingAvatarIndex);
        }

        private void SetAvatar(int avatarIndex)
        {
            if (_avatarImage == null) return;

            var sprite = Resources.Load<Sprite>($"{AvatarsResourcePath}{avatarIndex}");
            if (sprite != null)
                _avatarImage.sprite = sprite;
        }
    }

    [Serializable]
    public record PlayerGUISprites
    {
        public Sprite readySprite;
        public Sprite notReadySprite;
        public Sprite waitingSprite;
    }
}