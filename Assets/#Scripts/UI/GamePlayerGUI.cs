using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class GamePlayerGUI : MonoBehaviour
    {
        #region Scripts References

        [SerializeField] private SpriteFlipbook _skipFlipbook;

        #endregion

        #region UI References

        [Header("UI References")]
        [SerializeField] private Image _turnImage;
        [SerializeField] private TextMeshProUGUI _cardCountText;
        [SerializeField] private TextMeshProUGUI _usernameText;

        #endregion

        #region Unity Lifecycle 

        public void SetUserName(string username) => _usernameText.SetText(username);

        public void UpdateCardCount(int cardCount) => _cardCountText.text = $"{cardCount}";

        public void UpdateTurnTimer(float normalizedRemaining) => _turnImage.fillAmount = Mathf.Clamp01(normalizedRemaining);

        public void ResetTurnTimer() => _turnImage.fillAmount = 1.0f;

        public void PlaySkipEffect(Color tint) => _skipFlipbook.Play(tint);

        #endregion
    }
}