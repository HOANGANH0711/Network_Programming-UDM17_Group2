using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.GameLogic
{
    public enum CellState
    {
        Empty,  // O trong
        X,      // Nguoi choi 1
        O       // Nguoi choi 2
    }

    public class Cell
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public CellState State { get; set; }

        public Cell(int row, int col)
        {
            Row = row;
            Col = col;
            State = CellState.Empty;
        }
    }
}
