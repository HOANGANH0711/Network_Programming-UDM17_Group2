using Caro.Server.Core;
using Caro.Server.Storage;
using Caro.Shared.Game;
using Caro.Shared.Models;
using Caro.Shared.Network;
using Caro.Shared.Utils;
using Server.Handlers;
using Shared.Models;
using System;
using System.Collections.Generic;

namespace Caro.Server.Services
{
    public class GameService
    {
        private static GameService _instance;
        public static GameService Instance => _instance ??= new GameService();

        private readonly Dictionary<string, GameRoom> _playerRoomMap = new();
        private readonly Dictionary<GameRoom, BoardController> _boards = new();
        private readonly Dictionary<GameRoom, MatchmakingService> _roomMatchmaking = new();
        private readonly MatchHistoryRepository _historyRepo = new();

        private GameService() { }

        public GameRoom CreateRoom(ClientHandler p1, ClientHandler p2, MatchmakingService matchmaking, int boardSize = 20)
        {
            if (p1 == null || p2 == null || matchmaking == null)
                throw new ArgumentNullException();

            var room = new GameRoom(p1, p2, matchmaking);
            _boards[room] = new BoardController(boardSize);
            _roomMatchmaking[room] = matchmaking;
            _playerRoomMap[p1.PlayerInfo.Id] = room;
            _playerRoomMap[p2.PlayerInfo.Id] = room;

            room.StartGame();
            Console.WriteLine($"GameService: Room created — {p1.PlayerInfo.Name} vs {p2.PlayerInfo.Name}");
            return room;
        }

        public void HandlePacket(ClientHandler sender, Packet packet)
        {
            if (sender == null || packet == null) return;
            if (!_playerRoomMap.TryGetValue(sender.PlayerInfo.Id, out var room)) return;

            var opponent = sender == room.Player1 ? room.Player2 : room.Player1;

            switch (packet.Command)
            {
                case CommandType.Move:
                    HandleMove(room, sender, opponent, packet);
                    break;
                case CommandType.GameOver:
                    opponent?.SendPacket(packet);
                    FinishGame(room, sender.PlayerInfo.Name);
                    break;
                default:
                    opponent?.SendPacket(packet);
                    break;
            }
        }

        public void EndGame(GameRoom room) => RemoveRoom(room);

        private void HandleMove(GameRoom room, ClientHandler sender, ClientHandler opponent, Packet packet)
        {
            try
            {
                var move = Serializer.Deserialize<MoveInfo>(packet.Payload);
                if (move == null || !_boards.TryGetValue(room, out var board)) return;

                int playerNumber = sender == room.Player1 ? 1 : 2;

                if (!board.IsValidMove(move.X, move.Y) || !board.MakeMove(move.X, move.Y, playerNumber))
                {
                    Console.WriteLine($"Invalid move from {sender.PlayerInfo.Name} at ({move.X},{move.Y})");
                    return;
                }

                opponent?.SendPacket(packet);

                if (!CheckWin(board, move.X, move.Y, playerNumber)) return;

                var winnerName = sender.PlayerInfo.Name ?? playerNumber.ToString();
                var gameOverPacket = new Packet
                {
                    Command = CommandType.GameOver,
                    Payload = Serializer.Serialize(winnerName)
                };

                room.Player1.SendPacket(gameOverPacket);
                room.Player2.SendPacket(gameOverPacket);
                Console.WriteLine($"GameService: {winnerName} won — {room.Player1.PlayerInfo.Name} vs {room.Player2.PlayerInfo.Name}");

                _historyRepo.SaveMatch(room.Player1.PlayerInfo.Name, room.Player2.PlayerInfo.Name, winnerName);
                CloseRoom(room);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HandleMove error: {ex.Message}");
            }
        }

        private void FinishGame(GameRoom room, string winnerName)
        {
            try
            {
                if (room == null) return;
                _historyRepo.SaveMatch(room.Player1.PlayerInfo.Name, room.Player2.PlayerInfo.Name, winnerName);
                CloseRoom(room);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FinishGame error: {ex.Message}");
            }
        }

        private void CloseRoom(GameRoom room)
        {
            if (_roomMatchmaking.TryGetValue(room, out var matchmaking))
                matchmaking.EndRoom(room);
            RemoveRoom(room);
        }

        private void RemoveRoom(GameRoom room)
        {
            if (room == null) return;
            try
            {
                _boards.Remove(room);
                _roomMatchmaking.Remove(room);

                if (room.Player1?.PlayerInfo?.Id != null) _playerRoomMap.Remove(room.Player1.PlayerInfo.Id);
                if (room.Player2?.PlayerInfo?.Id != null) _playerRoomMap.Remove(room.Player2.PlayerInfo.Id);

                Console.WriteLine($"GameService: Room removed — {room.Player1.PlayerInfo.Name} vs {room.Player2.PlayerInfo.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RemoveRoom error: {ex.Message}");
            }
        }

        private bool CheckWin(BoardController board, int lastX, int lastY, int player)
        {
            if (board == null) return false;

            int[,] dirs = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };
            int size = board.Size;

            for (int d = 0; d < dirs.GetLength(0); d++)
            {
                int dx = dirs[d, 0], dy = dirs[d, 1];
                int count = 1;

                for (int s = -1; s <= 1; s += 2)
                {
                    int x = lastX + s * dx, y = lastY + s * dy;
                    while (x >= 0 && y >= 0 && x < size && y < size && board.GetCell(x, y) == player)
                    {
                        count++;
                        x += s * dx;
                        y += s * dy;
                    }
                }

                if (count >= 5) return true;
            }

            return false;
        }
    }
}