namespace CardClash
{
    #region Enums

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
        PlayMultipleCards = 6,
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

    public enum ScreenType
    {
        Room,
        Game,
        Login,
        Lobby,
        Settings,
        Loading,
        RoomInfo,
        HouseRules,
        ConnectionError,
    }

    public enum NotificationType
    {
        Red, 
        Blue,
        Green,
        Yellow,
        LastCard
    }

    public enum HouseRulePreset
    {
        Classic,
        Fast,
        Chaos,
        Custom
    }

    #endregion
}
