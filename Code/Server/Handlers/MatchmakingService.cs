using Caro.Server.Core;
using Caro.Shared.Models;
using Caro.Shared.Network;
using Caro.Shared.Utils;
using Shared.Models;
using System;
using System.Collections.Generic;

namespace Caro.Server.Services
{
    public class MatchmakingService
    {
        private readonly ServerManager _server;
        private readonly List<ClientHandler> _waitingPlayers = new();
        private readonly object _roomsLock = new();

        public MatchmakingService(ServerManager server)
        {
            _server = server;
        }

        public void HandlePacket(ClientHandler client, Packet packet)
        {
            if (packet == null) return;

            switch (packet.Command)
            {
                case CommandType.ChallengeRequest:
                    HandleFindMatch(client);
                    break;
                case CommandType.Challenge:
                    HandleDirectChallenge(client, packet);
                    break;
                case CommandType.Accept:
                    HandleChallengeAccept(client, packet);
                    break;
                case CommandType.Reject:
                    HandleChallengeReject(client, packet);
                    HandleFindMatch(client);
                    break;
                case CommandType.Move:
                case CommandType.Chat:
                case CommandType.GameOver:
                case CommandType.Surrender:
                case CommandType.TimerTick:
                case CommandType.TimeOut:
                    GameService.Instance.HandlePacket(client, packet);
                    break;
                case CommandType.Disconnect:
                    HandleDisconnect(client);
                    break;
            }
        }

        private void HandleFindMatch(ClientHandler client)
        {
            if (client.PlayerInfo.IsPlaying)
            {
                client.SendPacket(new Packet { Command = CommandType.LoginFailed, Data = "Bạn đang trong trận đấu, không thể tìm trận mới" });
                return;
            }

            if (_waitingPlayers.Contains(client)) return;

            Console.WriteLine($"{client.PlayerInfo.Name} is finding match...");

            if (_waitingPlayers.Count > 0)
            {
                var opponent = _waitingPlayers[0];
                _waitingPlayers.RemoveAt(0);

                opponent.PlayerInfo.IsPlaying = true;
                client.PlayerInfo.IsPlaying = true;

                GameService.Instance.CreateRoom(opponent, client, this);
                Console.WriteLine($"Match found: {opponent.PlayerInfo.Name} vs {client.PlayerInfo.Name}");
            }
            else
            {
                _waitingPlayers.Add(client);
                Console.WriteLine($"{client.PlayerInfo.Name} is waiting...");
            }
        }

        private void HandleDirectChallenge(ClientHandler client, Packet packet)
        {
            string targetName = packet.Data;
            if (string.IsNullOrWhiteSpace(targetName))
            {
                client.SendPacket(new Packet { Command = CommandType.InvalidInput, Data = "Tên người chơi không hợp lệ" });
                return;
            }

            var target = _server.GetClientByName(targetName);
            if (target == null)
            {
                client.SendPacket(new Packet { Command = CommandType.LoginFailed, Data = $"Người chơi '{targetName}' không tồn tại" });
                return;
            }

            if (target.PlayerInfo.IsPlaying)
            {
                client.SendPacket(new Packet { Command = CommandType.LoginFailed, Data = $"Người chơi '{targetName}' đang trong trận đấu" });
                return;
            }

            target.SendPacket(new Packet { Command = CommandType.Challenge, Data = client.PlayerInfo.Name });
            Console.WriteLine($"{client.PlayerInfo.Name} challenged {targetName}");
        }

        private void HandleChallengeAccept(ClientHandler client, Packet packet)
        {
            var challenger = _server.GetClientByName(packet.Data);
            if (challenger == null) return;

            challenger.PlayerInfo.IsPlaying = true;
            client.PlayerInfo.IsPlaying = true;

            GameService.Instance.CreateRoom(challenger, client, this);
        }

        private void HandleChallengeReject(ClientHandler client, Packet packet)
        {
            var challenger = _server.GetClientByName(packet.Data);
            challenger?.SendPacket(new Packet { Command = CommandType.Reject, Data = client.PlayerInfo.Name });
        }

        public void HandleDisconnect(ClientHandler client)
        {
            Console.WriteLine($"{client.PlayerInfo.Name} disconnected");
            lock (_roomsLock) { _waitingPlayers.Remove(client); }

            GameService.Instance.HandlePacket(client, new Packet { Command = CommandType.PlayerDisconnected });

            client.PlayerInfo.IsPlaying = false;
            _server.BroadcastPlayerList();
        }

        // Được gọi bởi GameService khi kết thúc room
        public void EndRoom(GameRoom room)
        {
            if (room.Player1 != null) room.Player1.PlayerInfo.IsPlaying = false;
            if (room.Player2 != null) room.Player2.PlayerInfo.IsPlaying = false;
            _server.BroadcastPlayerList();
        }
    }
}