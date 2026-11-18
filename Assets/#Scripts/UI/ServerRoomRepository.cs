using Mirror;
using System;
using System.Collections.Generic;

namespace CardClash
{
    internal sealed class ServerRoomRepository
    {
        internal readonly Dictionary<Guid, RoomInfo> OpenRooms = new();
        internal readonly Dictionary<Guid, CardDeck> RoomDecks = new();
        internal readonly Dictionary<Guid, CardGameController> MatchControllers = new();
        internal readonly Dictionary<NetworkConnectionToClient, Guid> PlayerRooms = new();
        internal readonly Dictionary<NetworkConnectionToClient, PlayerRoomInfo> PlayerInfos = new();
        internal readonly Dictionary<Guid, HashSet<NetworkConnectionToClient>> RoomConnections = new();
        internal readonly Dictionary<string, (Guid roomCode, PlayerRoomInfo info)> DisconnectedRoomPlayers = new();

        internal readonly List<NetworkConnectionToClient> WaitingConnections = new();

        internal void Clear()
        {
            PlayerRooms.Clear();
            OpenRooms.Clear();
            RoomConnections.Clear();
            WaitingConnections.Clear();
            RoomDecks.Clear();
            MatchControllers.Clear();
            DisconnectedRoomPlayers.Clear();
        }
    }
}
