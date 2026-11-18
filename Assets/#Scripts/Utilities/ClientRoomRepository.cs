using System;
using System.Collections.Generic;

namespace CardClash
{
    internal sealed class ClientRoomRepository
    {
        internal readonly Dictionary<Guid, RoomInfo> OpenRooms = new();

        internal Guid CurrentRoom { get; private set; } = Guid.Empty;

        internal Guid SelectedRoom { get; private set; } = Guid.Empty;

        internal bool IsOwner { get; private set; }

        internal void SetRoom(Guid roomCode, bool isOwner)
        {
            CurrentRoom = roomCode;
            IsOwner = isOwner;
        }

        internal void ClearRoom()
        {
            CurrentRoom = Guid.Empty;
            IsOwner = false;
        }

        internal void SelectRoom(Guid roomCode) => SelectedRoom = roomCode;

        internal void ClearSelectedRoom() => SelectedRoom = Guid.Empty;

        internal void SetRoomList(RoomInfo[] rooms)
        {
            OpenRooms.Clear();

            foreach (var room in rooms)
                OpenRooms[room.roomCode] = room;
        }

        internal void Clear()
        {
            OpenRooms.Clear();
            CurrentRoom = Guid.Empty;
            SelectedRoom = Guid.Empty;
            IsOwner = false;
        }
    }
}
