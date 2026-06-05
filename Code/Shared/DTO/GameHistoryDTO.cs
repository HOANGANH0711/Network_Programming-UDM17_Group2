using System;
using System.Collections.Generic;

namespace Shared.DTO
{
    public class GameHistoryDTO
    {
        public string GameID { get; set; } = string.Empty;
        public string Player1ID { get; set; } = string.Empty;
        public string Player2ID { get; set; } = string.Empty;
        public string WinnerID { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public List<string> MoveLog { get; set; } = new List<string>();
        public DateTime EndedAt { get; set; } = DateTime.Now;

        public override string ToString()
        {
            string winner = string.IsNullOrWhiteSpace(WinnerID) ? "Hoa" : WinnerID;
            return EndedAt.ToString("HH:mm dd/MM/yyyy") + " | " + Player1ID + " vs " + Player2ID + " | " + Result + " | " + winner;
        }
    }
}
