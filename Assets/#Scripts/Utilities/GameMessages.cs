using Mirror;
using System;

namespace CardClash
{
    #region Network Messages

    public struct ServerRoomMessage : NetworkMessage
    {
        public ServerRoomOperation serverRoomOperation;
        public Guid roomCode;
        public int maxPlayers;
        public int startingCards;
        public bool allowStacking;
        public bool noPowerCardFinish;
        public bool playSameNumber;
        public int maxCardsPerTurn;
    }

    public struct ServerDeckMessage : NetworkMessage
    {
        public ServerDeckOperation serverDeckOperation;
        public Guid RoomCode;
        public GameCard Card;
        public CardColor chosenWildColor;
        public GameCard[] Cards;
    }

    public struct ClientRoomMessage : NetworkMessage
    {
        public ClientRoomOperation clientRoomOperation;
        public Guid roomCode;
        public string errorMessage;
        public int maxPlayers;
        public RoomInfo[] roomInfo;
        public PlayerRoomInfo[] playerInfo;
        public bool allowStacking;
        public bool noPowerCardFinish;
        public bool playSameNumber;
        public int maxCardsPerTurn;
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

    public struct SetPlayerInfoMessage : NetworkMessage
    {
        public string Username;
        public string SessionId;
    }

    #endregion
}
