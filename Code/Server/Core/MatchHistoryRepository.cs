using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.DTO;
using System.Text.Json;

namespace Server.Core
{
    public class MatchHistoryRepository
    {
        // File lưu lịch sử trận đấu
        private string _filePath = "match_history.json";

        //Lay tat ca lich su tran dau 
        public List<GameDTO> GetAll()
        {
            // neu file chua ton tai thi tra ve danh sach trong
            if (!File.Exists(_filePath))
            {
                return new List<GameDTO>();
            }
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<GameDTO>>(json) ?? new List<GameDTO>();
        }

        // Lưu lịch sử trận đấu mới vào file
        public void Save(GameDTO game)
        {
            var list = GetAll();//lay danhs ach cu
            list.Add(game);// them tran moi vao

            //Ghi lai toan o danh sach xuong file
            File.WriteAllText(_filePath, JsonSerializer.Serialize(list));
            Console.WriteLine($"[History] Da luu lich su tran dau: {game.GameID}");
        }

        //Lay lich su theo PlayerID

        public List<GameDTO> GetByPlayer(string playerID)
        {
            return GetAll()
                .Where(g => g.Player1ID == playerID || g.Player2ID == playerID)
                .ToList();
        }

        //lay lich su theo GameID
        public GameDTO? GetByGameID (string gameID)
        {
            return GetAll()
                .FirstOrDefault(g => g.GameID == gameID);
        }
    }
}
