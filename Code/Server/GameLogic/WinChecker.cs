namespace Server.GameLogic
{
    public static class WinChecker
    {
        private static readonly (int dr, int dc)[] Directions =
        {
            (0, 1),
            (1, 0),
            (1, 1),
            (1, -1)
        };

        public static bool HasWinner(Board board, int row, int col, CellValue value)
        {
            var raw = (int)value;
            foreach (var (dr, dc) in Directions)
            {
                var count = 1
                    + CountDirection(board, row, col, dr, dc, raw)
                    + CountDirection(board, row, col, -dr, -dc, raw);
                if (count >= 5)
                    return true;
            }
            return false;
        }

        public static bool HasWinner(Board board, int row, int col, string symbol) =>
            HasWinner(board, row, col, CellValueExtensions.FromSymbol(symbol));

        private static int CountDirection(Board board, int row, int col, int dr, int dc, int raw)
        {
            var count = 0;
            var r = row + dr;
            var c = col + dc;
            while (r >= 0 && r < Board.Size && c >= 0 && c < Board.Size && board.GetRaw(r, c) == raw)
            {
                count++;
                r += dr;
                c += dc;
            }
            return count;
        }

        internal static int CountLine(Board board, int row, int col, int dr, int dc, int value, out bool openEnd)
        {
            var count = 0;
            var r = row + dr;
            var c = col + dc;
            while (r >= 0 && r < Board.Size && c >= 0 && c < Board.Size && board.GetRaw(r, c) == value)
            {
                count++;
                r += dr;
                c += dc;
            }
            openEnd = r >= 0 && r < Board.Size && c >= 0 && c < Board.Size && board.GetRaw(r, c) == 0;
            return count;
        }

        internal static int EvaluatePoint(Board board, int row, int col, int value)
        {
            var score = 0;
            foreach (var (dr, dc) in Directions)
            {
                var forward = CountLine(board, row, col, dr, dc, value, out var openF);
                var backward = CountLine(board, row, col, -dr, -dc, value, out var openB);
                var length = 1 + forward + backward;
                var openEnds = (openF ? 1 : 0) + (openB ? 1 : 0);
                score += ScorePattern(length, openEnds);
            }
            return score;
        }

        internal static int ScorePattern(int length, int openEnds)
        {
            if (length >= 5) return 10_000_000;
            if (length == 4 && openEnds == 2) return 1_000_000;
            if (length == 4 && openEnds == 1) return 120_000;
            if (length == 3 && openEnds == 2) return 45_000;
            if (length == 3 && openEnds == 1) return 5_000;
            if (length == 2 && openEnds == 2) return 1_200;
            if (length == 2 && openEnds == 1) return 150;
            if (length == 1 && openEnds == 2) return 20;
            return 1;
        }
    }
}
