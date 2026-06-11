using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Shared.DTO;
using Shared.Models;

namespace Server.Rooms
{
    // Represents a room with at most 2 players
    public class GameRoom
    {
        private readonly object _lock = new object();

        public string RoomID { get; }
        public string RoomName { get; }
        public RoomDTO Info { get; }

        public bool IsFull => !string.IsNullOrEmpty(Info.Player1ID) && !string.IsNullOrEmpty(Info.Player2ID);
        public bool IsPlaying
        {
            get { return Info.IsPlaying; }
            private set { Info.IsPlaying = value; }
        }

        public GameRoom(string roomId, string roomName, string ownerId)
        {
            RoomID = roomId;
            RoomName = roomName;
            Info = new RoomDTO
            {
                RoomID = roomId,
                RoomName = roomName,
                OwnerID = ownerId,
                Player1ID = ownerId,
                Player2ID = string.Empty,
                IsPlaying = false,
                IsFull = false
            };
        }

        // Try to add a player; returns true if added
        public bool TryJoin(string playerId)
        {
            lock (_lock)
            {
                if (IsFull) return false;

                if (string.IsNullOrEmpty(Info.Player1ID))
                {
                    Info.Player1ID = playerId;
                }
                else if (string.IsNullOrEmpty(Info.Player2ID))
                {
                    Info.Player2ID = playerId;
                }

                Info.IsFull = IsFull;
                return true;
            }
        }

        // Remove a player from the room
        public void Leave(string playerId)
        {
            lock (_lock)
            {
                if (Info.Player1ID == playerId) Info.Player1ID = string.Empty;
                if (Info.Player2ID == playerId) Info.Player2ID = string.Empty;

                Info.IsFull = IsFull;
                // If players left, stop playing
                if (string.IsNullOrEmpty(Info.Player1ID) || string.IsNullOrEmpty(Info.Player2ID))
                    IsPlaying = false;
            }
        }

        public void StartGame()
        {
            lock (_lock)
            {
                IsPlaying = true;
            }
        }

        public void EndGame()
        {
            lock (_lock)
            {
                IsPlaying = false;
                // Optionally keep players to allow rematch; cleanup is managed by GameRoomManager.PlayerLeft
            }
        }

        public bool Contains(string playerId)
        {
            return Info.Player1ID == playerId || Info.Player2ID == playerId;
        }
    }
}
