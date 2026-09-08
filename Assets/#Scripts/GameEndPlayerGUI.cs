using TMPro;
using UnityEngine;

namespace UNO
{
    public class GameEndPlayerGUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _playerNameText;
        [SerializeField] private TextMeshProUGUI _playerScoreText;


        public void SetPlayerInfo(string playerName, int playerScore)
        {
            _playerNameText.SetText(playerName);
            _playerScoreText.SetText($"{playerScore}");
        }
    }
}