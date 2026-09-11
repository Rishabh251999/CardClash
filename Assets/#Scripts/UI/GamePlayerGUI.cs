
using TMPro;
using UnityEngine;

namespace CardClash
{
    public class GamePlayerGUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _cardCountText;

        public void UpdateCardCount(int cardCount)
        {
            _cardCountText.text = $"{cardCount}";
        }
    }
}