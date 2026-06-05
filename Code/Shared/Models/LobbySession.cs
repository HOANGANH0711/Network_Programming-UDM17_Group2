using System.Collections.Concurrent;
using Shared.Enums;

namespace Shared.Models
{
    public class LobbySession
    {
        private readonly ConcurrentDictionary<string, Player> _players = new();

        public bool AddPlayer(Player player)
        {
            return _players.TryAdd(player.PlayerId, player);
        }

        public bool RemovePlayer(string playerId)
        {
            return _players.TryRemove(playerId, out _);
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