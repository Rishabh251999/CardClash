using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    public class GamePlayerGUI : MonoBehaviour
    {
        #region UI References

        [Header("UI References")]
        [SerializeField] private Image _turnImage;
        [SerializeField] private TextMeshProUGUI _cardCountText;

        #endregion

        #region Unity Lifecycle 

        public void UpdateCardCount(int cardCount) => _cardCountText.text = $"{cardCount}";

        public void SetTurnIndicator(float normalizedAmount) => _turnImage.fillAmount = Mathf.Clamp01(normalizedAmount);

        #endregion
    }
}