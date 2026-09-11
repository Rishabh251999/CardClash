using Mirror;
using TMPro;
using UnityEngine;

namespace UNO
{
    public class PlayerGUI : MonoBehaviour
    {
        private readonly Color32 ReadyColor = new(34, 197, 94, 255);   
        private readonly Color32 NotReadyColor = new(245, 158, 11, 255);

        [SerializeField] private TextMeshProUGUI _readyText;
        [SerializeField] private TextMeshProUGUI _playerNameText;

        [SerializeField] private GameObject _roomOwnerGameObject;

        [ClientCallback]
        public void SetPlayerInfo(PlayerRoomInfo info)
        {
            _playerNameText.SetText($"{info.playerName}");

            _readyText.SetText(info.isReady ? "Ready" : "Not Ready");
            _readyText.color = info.isReady ? ReadyColor : NotReadyColor;

            _roomOwnerGameObject.SetActive(info.isOwner);
        }
    }
}