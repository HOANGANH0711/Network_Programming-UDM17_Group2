using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.GameLogic
{
    public class Board
    {
        public const int Size = 15;
        private Cell[,] _cells;

        public Board()
        {
            _cells = new Cell[Size, Size];
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    _cells[r, c] = new Cell(r, c);
        }

        public Cell GetCell(int row, int col) => _cells[row, col];

        public bool IsValidMove(int row, int col)
        {
            return row >= 0 && row < Size &&
                   col >= 0 && col < Size &&
                   _cells[row, col].State == CellState.Empty;
        }

        public bool PlaceMove(int row, int col, CellState player)
        {
            if (!IsValidMove(row, col)) return false;
            _cells[row, col].State = player;
            return true;
        }

        public void Reset()
        {
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    _cells[r, c].State = CellState.Empty;

        }
        public bool IsFull()
        {
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    if (_cells[r, c].State == CellState.Empty)
                        return false;
                }
            }
            return true;
        }
    }
}
