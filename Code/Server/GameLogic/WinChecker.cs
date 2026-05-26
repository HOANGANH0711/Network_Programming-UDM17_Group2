using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.GameLogic
{
    public class WinChecker
    {
        private static readonly (int dr, int dc)[] Directions =
        {
        (0, 1),   // Ngang →
        (1, 0),   // Doc ↓
        (1, 1),   // Chéo ↘
        (1, -1)   // Chéo ↗
    };

        public static bool CheckWin(Board board, int row, int col, CellState player)
        {
            foreach (var (dr, dc) in Directions)
            {
                int count = 1;
                count += CountDirection(board, row, col, dr, dc, player);
                count += CountDirection(board, row, col, -dr, -dc, player);

                if (count >= 5) return true;
            }
            return false;
        }

        private static int CountDirection(Board board, int row, int col,
                                          int dr, int dc, CellState player)
        {
            int count = 0;
            int r = row + dr, c = col + dc;

            while (r >= 0 && r < Board.Size &&
                   c >= 0 && c < Board.Size &&
                   board.GetCell(r, c).State == player)
            {
                count++;
                r += dr;
                c += dc;
            }
            return count;
        }
    }
}
