using Shared.DTO;

namespace Server.Handlers
{
    public class MatchmakingService
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, UserDTO> _users = new Dictionary<string, UserDTO>();
        private readonly Dictionary<string, InviteDto> _pendingInvites = new Dictionary<string, InviteDto>();

        public UserDTO Login(string playerId, string username)
        {
            username = string.IsNullOrWhiteSpace(username) ? $"Player {playerId}" : username.Trim();
            var user = new UserDTO
            {
                UserID = playerId,
                UserName = username,
                IsOnline = true,
                IsInGame = false
            };

            lock (_lock)
                _users[playerId] = user;

            return user;
        }

        public void Logout(string playerId)
        {
            lock (_lock)
            {
                _users.Remove(playerId);
                foreach (var key in _pendingInvites.Keys.Where(k => k.StartsWith(playerId + ":", StringComparison.Ordinal) || k.EndsWith(":" + playerId, StringComparison.Ordinal)).ToList())
                    _pendingInvites.Remove(key);
            }
        }

        public List<UserDTO> GetPlayers()
        {
            lock (_lock)
            {
                return _users.Values
                    .Select(u => new UserDTO
                    {
                        UserID = u.UserID,
                        UserName = u.UserName,
                        IsOnline = u.IsOnline,
                        IsInGame = u.IsInGame
                    })
                    .ToList();
            }
        }

        public string GetName(string playerId)
        {
            if (playerId == Server.GameLogic.ActiveGame.BotId)
                return "Bot";

            lock (_lock)
                return _users.TryGetValue(playerId, out var user) ? user.UserName : playerId;
        }

        public void MarkInGame(string playerId, bool isInGame)
        {
            lock (_lock)
            {
                if (_users.TryGetValue(playerId, out var user))
                    user.IsInGame = isInGame;
            }
        }

        public InviteDto CreateInvite(string fromPlayerId, InviteDto request)
        {
            var invite = new InviteDto
            {
                FromPlayerId = fromPlayerId,
                FromPlayerName = GetName(fromPlayerId),
                ToPlayerId = request.ToPlayerId,
                ToPlayerName = GetName(request.ToPlayerId),
                TurnSeconds = request.TurnSeconds,
                InviterSymbol = request.InviterSymbol == "O" ? "O" : "X"
            };

            lock (_lock)
                _pendingInvites[InviteKey(invite.FromPlayerId, invite.ToPlayerId)] = invite;

            return invite;
        }

        public InviteDto? TakeInvite(string fromPlayerId, string toPlayerId)
        {
            lock (_lock)
            {
                var key = InviteKey(fromPlayerId, toPlayerId);
                if (!_pendingInvites.TryGetValue(key, out var invite))
                    return null;

                _pendingInvites.Remove(key);
                return invite;
            }
        }

        private static string InviteKey(string fromPlayerId, string toPlayerId) => $"{fromPlayerId}:{toPlayerId}";
    }
}
