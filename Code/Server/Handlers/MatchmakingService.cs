using Server.Core;
using Shared.DTO;
using Shared.Enums;
using Shared.Models;

namespace Server.Handlers
{
    public class MatchmakingService
    {
        private const string BotId = "BOT";
        private readonly object _lock = new object();
        private readonly Dictionary<string, UserDTO> _users = new();
        private readonly Dictionary<string, InviteDto> _pendingInvites = new();
        private readonly Func<string, ClientHandler?> _findClient;
        private readonly Func<IReadOnlyList<ClientHandler>> _getConnectedClients;
        private readonly Func<InviteDto, Task> _startOnlineGame;

        public MatchmakingService(
            Func<string, ClientHandler?> findClient,
            Func<IReadOnlyList<ClientHandler>> getConnectedClients,
            Func<InviteDto, Task> startOnlineGame)
        {
            _findClient = findClient;
            _getConnectedClients = getConnectedClients;
            _startOnlineGame = startOnlineGame;
        }

        public async Task LoginAsync(ClientHandler client, string username)
        {
            username = string.IsNullOrWhiteSpace(username) ? $"Player {client.ClientId}" : username.Trim();
            lock (_lock)
            {
                _users[client.ClientId] = new UserDTO
                {
                    UserID = client.ClientId,
                    UserName = username,
                    IsOnline = true,
                    IsInGame = false
                };
            }

            await client.SendPacketAsync(PacketHelper.Create(CommandType.SUCCESS, "Logged in", client.ClientId));
            await BroadcastLobbyAsync();
        }

        public async Task SendInviteAsync(ClientHandler client, string data)
        {
            var invite = Serializer.Deserialize<InviteDto>(data);
            if (invite == null)
                return;

            lock (_lock)
            {
                invite.FromPlayerId = client.ClientId;
                invite.FromPlayerName = GetName(client.ClientId);
                invite.ToPlayerName = GetName(invite.ToPlayerId);
                _pendingInvites[$"{invite.FromPlayerId}:{invite.ToPlayerId}"] = invite;
            }

            var target = _findClient(invite.ToPlayerId);
            if (target != null)
                await target.SendPacketAsync(PacketHelper.Create(CommandType.INVITE, invite, client.ClientId));
        }

        public async Task HandleInviteResponseAsync(ClientHandler client, string data)
        {
            var response = Serializer.Deserialize<InviteResponseDto>(data);
            if (response == null)
                return;

            InviteDto? invite = null;
            lock (_lock)
            {
                _pendingInvites.TryGetValue($"{response.FromPlayerId}:{client.ClientId}", out invite);
                _pendingInvites.Remove($"{response.FromPlayerId}:{client.ClientId}");
            }

            var inviter = _findClient(response.FromPlayerId);
            if (!response.Accepted)
            {
                if (inviter != null)
                    await inviter.SendPacketAsync(PacketHelper.Create(CommandType.INVITE_RESPONSE, response));
                return;
            }

            if (invite != null)
                await _startOnlineGame(invite);
        }

        public async Task SendLobbyAsync(ClientHandler client)
        {
            await client.SendPacketAsync(PacketHelper.Create(CommandType.PLAYER_LIST, GetUsers()));
        }

        public async Task BroadcastLobbyAsync()
        {
            var packet = PacketHelper.Create(CommandType.LOBBY_UPDATE, GetUsers());
            var clients = _getConnectedClients();
            await Task.WhenAll(clients.Select(c => c.SendPacketAsync(packet)));
        }

        public void RemovePlayer(string clientId)
        {
            lock (_lock)
                _users.Remove(clientId);
        }

        public void MarkInGame(string playerId, bool isInGame)
        {
            lock (_lock)
            {
                if (_users.TryGetValue(playerId, out var user))
                    user.IsInGame = isInGame;
            }
        }

        public string GetName(string playerId)
        {
            if (playerId == BotId)
                return "Bot";
            lock (_lock)
                return _users.TryGetValue(playerId, out var user) ? user.UserName : playerId;
        }

        private List<UserDTO> GetUsers()
        {
            lock (_lock)
                return _users.Values.Select(u => new UserDTO
                {
                    UserID = u.UserID,
                    UserName = u.UserName,
                    IsOnline = u.IsOnline,
                    IsInGame = u.IsInGame
                }).ToList();
        }
    }
}
