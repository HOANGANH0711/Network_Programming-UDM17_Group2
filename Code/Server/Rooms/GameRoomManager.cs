using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.DTO;

namespace Server.Rooms
{
    public class GameRoomManager
    {
        private readonly ConcurrentDictionary<string, GameRoom> _rooms = new ConcurrentDictionary<string, GameRoom>();

        public event EventHandler<RoomEventArgs>? RoomCreated;
        public event EventHandler<RoomEventArgs>? RoomRemoved;

        public GameRoom CreateRoom(string roomName, string ownerId)
        {
            string roomId = Guid.NewGuid().ToString().Substring(0, 8);
            var room = new GameRoom(roomId, roomName, ownerId);
            if (_rooms.TryAdd(roomId, room))
            {
                RoomCreated?.Invoke(this, new RoomEventArgs { Room = room.Info });
                return room;
            }

            throw new InvalidOperationException("Unable to create room");
        }

        public bool RemoveRoom(string roomId)
        {
            if (_rooms.TryRemove(roomId, out var room))
            {
                RoomRemoved?.Invoke(this, new RoomEventArgs { Room = room.Info });
                return true;
            }
            return false;
        }

        public IEnumerable<RoomDTO> GetRoomList()
        {
            return _rooms.Values.Select(r => r.Info).ToList();
        }

        public bool TryJoinRoom(string roomId, string playerId)
        {
            if (_rooms.TryGetValue(roomId, out var room))
            {
                return room.TryJoin(playerId);
            }
            return false;
        }

        public void PlayerLeft(string playerId)
        {
            // Remove player from any room they are in; if room becomes empty, remove it
            var roomsContaining = _rooms.Values.Where(r => r.Contains(playerId)).ToList();
            foreach (var room in roomsContaining)
            {
                room.Leave(playerId);
                if (string.IsNullOrEmpty(room.Info.Player1ID) && string.IsNullOrEmpty(room.Info.Player2ID))
                {
                    RemoveRoom(room.RoomID);
                }
            }
        }
    }

    public class RoomEventArgs : EventArgs
    {
        public RoomDTO? Room { get; set; }
    }
}
