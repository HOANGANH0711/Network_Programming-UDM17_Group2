namespace Server.GameLogic
{
    public static class WinChecker
    {
        public static bool HasWinner(int[][] board, int row, int col, string symbol)
        {
            var value = symbol == "X" ? 1 : 2;
            var directions = new[] { (0, 1), (1, 0), (1, 1), (1, -1) };
            foreach (var (dr, dc) in directions)
            {
                var count = 1 + Count(board, row, col, dr, dc, value)
                              + Count(board, row, col, -dr, -dc, value);
                if (count >= 5)
                    return true;
            }
            return false;
        }

        public static int Count(int[][] board, int row, int col, int dr, int dc, int value)
        {
            var count = 0;
            var r = row + dr;
            var c = col + dc;
            while (r >= 0 && r < Board.Size && c >= 0 && c < Board.Size && board[r][c] == value)
            {
                count++;
                r += dr;
                c += dc;
            }
            return count;
        }
    }
}