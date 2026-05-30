using Shared.Enums;

namespace Shared.Models
{
    public class LobbySession
    {
        private readonly Dictionary<string, Player> _players = new();

        public bool AddPlayer(Player player)
        {
            if (_players.ContainsKey(player.PlayerId)) return false;
            _players[player.PlayerId] = player;
            return true;
        }

        public bool RemovePlayer(string playerId)
        {
            return _players.Remove(playerId);
        }

        public List<Player> GetOnlinePlayers()
        {
            return _players.Values.ToList();
        }

        public Player GetPlayer(string playerId)
        {
            _players.TryGetValue(playerId, out var player);
            return player;
        }

        public bool UpdateStatus(string playerId, PlayerStatus status)
        {
            if (!_players.TryGetValue(playerId, out var player)) return false;
            player.Status = status;
            return true;
        }

        public bool IsOnline(string playerId)
        {
            return _players.ContainsKey(playerId);
        }

        public int OnlineCount => _players.Count;
    }
}