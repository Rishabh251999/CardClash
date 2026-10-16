using Mirror;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CardClash
{
    public class RoomGUI : MonoBehaviour
    {
        private GameManager _gameManager;
        [SerializeField] private RoomGUISprites _roomGUISprites;

        [SerializeField] private Image _roomIndicatorImage;
        [SerializeField] private TextMeshProUGUI _roomCodeText;
        [SerializeField] private TextMeshProUGUI _playerCountText;
        [SerializeField] private Button _joinRoomButton;

        private Guid _roomCode;
        private UnityAction _requestJoinRoom;

        private void Awake()
        {
            _gameManager = FindAnyObjectByType<GameManager>();
        }

        private void OnDestroy()
        {
            _joinRoomButton.onClick.RemoveListener(_requestJoinRoom);
        }

        [ClientCallback]
        public void OnRoomClicked(bool isOn)
        {
            var roomCode = isOn ? _roomCode : Guid.Empty;

            _gameManager.SelectRoom(roomCode);
        }

        [ClientCallback]
        public Guid GetRoomCode() => _roomCode;

        [ClientCallback]
        public void SetRoomInfo(RoomInfo roomInfo, UnityAction requestJoinRoom)
        {
            _roomCode = roomInfo.roomCode;
            _roomCodeText.text = $"Room: {_roomCode.ToString()[..6].ToUpper()}";
            _playerCountText.text = $"{roomInfo.playerCount} / {roomInfo.maxPlayers} Players";

            _joinRoomButton.onClick.RemoveListener(OnJoinButtonClicked);

            _requestJoinRoom = requestJoinRoom;
            _joinRoomButton.onClick.AddListener(OnJoinButtonClicked);

            UpdateRoomIndicator(roomInfo);
        }

        [ClientCallback]
        private void OnJoinButtonClicked()
        {
            _joinRoomButton.interactable = false;

            _gameManager.SelectRoom(_roomCode);
            _requestJoinRoom?.Invoke();
        }

        [ClientCallback]
        private void UpdateRoomIndicator(RoomInfo roomInfo)
        {
            bool isFull = roomInfo.playerCount >= roomInfo.maxPlayers;

            if (roomInfo.isStarted)
            {
                _roomIndicatorImage.sprite = _roomGUISprites.redSprite;
                _joinRoomButton.interactable = false;
            }
            else if (isFull)
            {
                _roomIndicatorImage.sprite = _roomGUISprites.yellowSprite;
                _joinRoomButton.interactable = false;
            }
            else
            {
                _roomIndicatorImage.sprite = _roomGUISprites.greenSprite;
                _joinRoomButton.interactable = true;
            }
        }
    }

    [Serializable]
    public record RoomGUISprites
    {
        public Sprite greenSprite;
        public Sprite yellowSprite;
        public Sprite redSprite;

        public Sprite room_available;
        public Sprite room_full;
    }
}