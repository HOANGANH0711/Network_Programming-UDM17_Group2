using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.GameLogic
{
    public class Move
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public CellState Player { get; set; }
        public DateTime Timestamp { get; set; }

        public Move(int row, int col, CellState player)
        {
            Row = row;
            Col = col;
            Player = player;
            Timestamp = DateTime.Now;
        }
    }
}
