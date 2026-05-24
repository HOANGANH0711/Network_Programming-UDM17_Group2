using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTO
{
    public class GameDTO
    {
        public string GameID { get; set; }
        public string Player1ID { get; set; }
        public string Player2ID { get; set; }
        public string CurrentTurnID { get; set; }
        public int[,] Board { get; set; }
        public bool IsGameOver { get; set; }
        public string WinnerID { get; set; }
        public int TimeRemaining { get; set; }
        public GameDTO()
        {
            Board = new int[15, 15];
            IsGameOver = false;
        }
    }
}
