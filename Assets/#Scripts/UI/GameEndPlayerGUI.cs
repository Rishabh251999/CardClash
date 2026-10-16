using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class GameEndPlayerGUI : MonoBehaviour
    {
        #region Constants/Readonly

        private const string AvatarsResourcePath = "Avatars/";

        #endregion

        #region UI References

        [SerializeField] private Image _playerAvatarImage;
        [SerializeField] private Image _image;
        [Space(2.5f)]
        [SerializeField] private TextMeshProUGUI _playerIndexText;
        [SerializeField] private TextMeshProUGUI _playerNameText;
        [SerializeField] private TextMeshProUGUI _playerScoreText;

        #endregion

        #region Sprites

        [Header("Sprites")]
        [Space(5f)]
        [SerializeField] private Sprite _winnerSprite;
        [SerializeField] private Sprite _normalSprite;

        #endregion

        public void SetPlayerInfo(int playerIndex, string playerName, int playerScore, int avatarIndex)
        {
            _playerIndexText.SetText($"{playerIndex}");
            _playerNameText.SetText(playerName);
            _playerScoreText.SetText($"{playerScore}");

            bool isTopRank = playerIndex == 1;

            if (_image != null)
                _image.sprite = isTopRank ? _winnerSprite : _normalSprite;

            SetAvatar(avatarIndex);
        }

        private void SetAvatar(int avatarIndex)
        {
            if (_playerAvatarImage == null) return;

            var sprite = Resources.Load<Sprite>($"{AvatarsResourcePath}{avatarIndex}");
            if (sprite != null)
                _playerAvatarImage.sprite = sprite;
        }
    }
}