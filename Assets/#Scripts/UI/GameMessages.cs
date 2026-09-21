using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardClash
{
    #region Network Messages

    /// <summary>
    /// Message sent from client to server for room operations
    /// </summary>
    public struct ServerRoomMessage : NetworkMessage
    {
        public ServerRoomOperation serverRoomOperation;
        public Guid roomCode;

        public int maxPlayers;
        public int startingCards;
    }

    public struct ServerDeckMessage : NetworkMessage
    {
        public ServerDeckOperation serverDeckOperation;
        public Guid RoomCode;
        public GameCard Card;
        public CardColor chosenWildColor;
    }

    /// <summary>
    /// Message sent from server to client
    /// </summary>
    public struct ClientRoomMessage : NetworkMessage
    {
        public ClientRoomOperation clientRoomOperation;
        public Guid roomCode;

        public string errorMessage;

        public int maxPlayers;

        public RoomInfo[] roomInfo;
        public PlayerRoomInfo[] playerInfo;
    }

    public struct ClientDeckMessage : NetworkMessage
    {
        public ClientDeckOperation clientDeckOperation;
        public Guid RoomCode;
        public GameCard[] Cards;
        public GameCard TopDiscardCard;
        public int DrawPileCount;
        public string errorMessage;
        public bool CanPlayDrawnCard;
    }

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
    }

    /// <summary>
    /// Information about a player in a room
    /// </summary>
    [Serializable]
    public struct PlayerRoomInfo
    {
        public int playerId;
        public int cardCount;
        public bool isReady;
        public bool isOwner;
        public Guid roomCode;
        public string playerName;
    }

    [Serializable]
    public struct PlayerGameInfo
    {
        public bool isOwner;
        public bool hasCalledLastCard;

        public int cardCount;
        public uint connectionId;
        public string playerName;
    }

    [Serializable]
    public struct GameEndPlayerInfo
    {
        public string PlayerName;
        public int PlayerScore;
    }

    /// <summary>
    /// Operations the server can perform on rooms
    /// </summary>
    public enum ServerRoomOperation : byte
    {
        None,
        Create,
        Join,
        Leave,
        List,
        Start,
        Ready,
        Cancel
    }

    public enum ServerDeckOperation : byte
    {
        None = 0,
        DrawCard = 1,  
        PlayCard = 2, 
        PassTurn = 3,
        QuitMatch = 4,
        CallLastCard = 5,
    }

    /// <summary>
    /// Operations the client receives about rooms
    /// </summary>
    public enum ClientRoomOperation : byte
    {
        None,
        Created,
        Joined,
        Left,
        Cancelled,
        UpdateRoom,
        ListUpdated,
        Started,
        MatchEndedByOwner, 
        MatchEndedByTimeout,
        Error
    }

    public enum ClientDeckOperation : byte
    {
        None = 0,
        DeckCreated = 1,
        CardDealt = 2,
        CardPlayed = 3,
        CardDrawn = 4,
        DeckReshuffled = 5,
        Error = 6,
        StackedDraw = 7,
        PlayerQuit = 8,
        LastCardCalled = 9,   
        LastCardPenalty = 10,
    }

    #endregion

    #region Card

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

    public class CustomSyncDictionary<TKey, TValue> : Mirror.SyncIDictionary<TKey, TValue>
    {
        public CustomSyncDictionary(IDictionary<TKey, TValue> objects) : base(objects) 
        { 

        }

        public bool SetLocalValue(TKey key, TValue value)
        {
            if (ContainsKey(key))
            {
                objects[key] = value;
                return true;
            }
            return false;
        }
    }

    public enum CardColor : byte
    {
        None = 0,
        Red = 1,
        Yellow = 2,
        Green = 3,
        Blue = 4,
    }

    public enum CardType : byte
    {
        Number = 0,
        Skip = 1,
        Reverse = 2,
        DrawTwo = 3,
        Wild = 4,
        WildDrawFour = 5
    }

    [Serializable]
    public struct GameCard
    {
        public byte Id;

        public CardColor Color;

        public CardType Type;

        public byte FaceValue;

        public override readonly string ToString()
        {
            string colorStr = Color == CardColor.None ? "" : $"{Color} ";
            string typeStr = Type == CardType.Number
                ? FaceValue.ToString()
                : TypeToString(Type);
            return $"{colorStr}{typeStr}";
        }

        private readonly string TypeToString(CardType type) => type switch
        {
            CardType.Skip => "Skip",
            CardType.Reverse => "Reverse",
            CardType.DrawTwo => "Draw",
            CardType.Wild => "Wild",
            CardType.WildDrawFour => "WildDrawFour",
            _ => "Unknown"
        };
    }

    #endregion

    #region struct

    [Serializable]
    public struct PanelEntry
    {
        public ScreenType ScreenType;
        public CanvasGroup CanvasGroup;
    }

    public struct SetPlayerInfoMessage : NetworkMessage
    {
        public string Username;
    }

    #endregion

    #region Enums

    public enum ScreenType
    {
        Loading,
        Login,
        Lobby,
        RoomInfo,
        Room,
        ConnectionError,
        Game,
    }

    #endregion
}