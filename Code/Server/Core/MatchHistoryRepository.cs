using Server.GameLogic;
using Shared.DTO;
using Shared.Models;

namespace Server.Core
{
    public class MatchHistoryRepository
    {
        private readonly object _lock = new object();
        private readonly string _filePath;
        private readonly List<HistoryItemDto> _items;

        public MatchHistoryRepository()
        {
            _filePath = Path.Combine(AppContext.BaseDirectory, "history_items.json");
            _items = Load();
        }

        public List<HistoryItemDto> GetByPlayer(string playerId)
        {
            lock (_lock)
                return _items.Where(h => h.GameID.StartsWith(playerId + "-", StringComparison.Ordinal)).ToList();
        }

        public void SaveGame(ActiveGame game, Func<string, string> getPlayerName)
        {
            lock (_lock)
            {
                if (game.PlayerXID != ActiveGame.BotId)
                    _items.Add(game.ToHistory(game.PlayerXID, getPlayerName(game.PlayerOID)));

                if (game.PlayerOID != ActiveGame.BotId)
                    _items.Add(game.ToHistory(game.PlayerOID, getPlayerName(game.PlayerXID)));

                File.WriteAllText(_filePath, Serializer.Serialize(_items));
            }
        }

        private List<HistoryItemDto> Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new List<HistoryItemDto>();

                return Serializer.Deserialize<List<HistoryItemDto>>(File.ReadAllText(_filePath)) ?? new List<HistoryItemDto>();
            }
            catch
            {
                return new List<HistoryItemDto>();
            }
        }
    }
}
