using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    #region Network Data

    /// <summary>
    /// Information about a room
    /// </summary>
    [Serializable]
    public struct RoomInfo
    {
        public Guid roomCode;

        public int playerCount;
        public int maxPlayers;
        public int startingCards;

        public bool isStarted;
        public bool allowStacking;
        public bool noPowerCardFinish;
        public bool playSameNumber;

        public int maxCardsPerTurn;
    }

    /// <summary>
    /// Information about a player in a room
    /// </summary>
    [Serializable]
    public struct PlayerRoomInfo
    {
        public int avatarId;
        public int playerId;
        public int cardCount;

        public bool isReady;
        public bool isOwner;

        public Guid roomCode;

        public string playerName;
        public string sessionId;
    }

    [Serializable]
    public struct PlayerGameInfo
    {
        public bool isOwner;
        public bool hasCalledLastCard;

        public int avatarId;
        public int cardCount;
        public uint connectionId;

        public string playerName;
    }

    [Serializable]
    public struct GameEndPlayerInfo
    {
        public string PlayerName;
        public int PlayerScore;
        public int AvatarId;
    }

    #endregion

    #region Card Data

    public struct CardData
    {
        public string name;
        public byte value;
        public Texture sprite;
    }

    public struct PlayerCardData
    {
        public byte cardId;

        [NonSerialized]
        public GameObject clientCardObject;
    }

    #endregion

    #region struct

    [Serializable]
    public struct PanelEntry
    {
        public ScreenType ScreenType;
        public CanvasGroup CanvasGroup;
    }

    [Serializable]
    public struct PendingDrawState
    {
        public int Count;
        public CardType Type;
    }

    [Serializable]
    public struct NumberCardSprite
    {
        public CardColor Color;
        [Range(0, 9)] public byte FaceValue;
        public Sprite Sprite;
    }

    [Serializable]
    public struct ActionCardSprite
    {
        public CardColor Color;
        public CardType Type;
        public Sprite Sprite;
    }

    [Serializable]
    public struct ToggleData
    {
        public TextMeshProUGUI statusText;
        public Toggle toggle;
    }

    #endregion
}
