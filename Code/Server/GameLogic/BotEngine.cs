using Shared.DTO;

namespace Server.GameLogic
{
    public class BotEngine
    {
        private readonly Random _random = new Random();

        public (int row, int col) ChooseMove(int[][] board, List<MoveRecordDto> moves, string botSymbol, string difficulty)
        {
            var humanSymbol = botSymbol == "X" ? "O" : "X";

            if (moves.Count == 0)
                return (7, 7);

            if (difficulty != "Easy")
            {
                var win = FindFinishingMove(board, moves, botSymbol);
                if (win.row >= 0) return win;

                var block = FindFinishingMove(board, moves, humanSymbol);
                if (block.row >= 0) return block;
            }

            if (difficulty == "Hard")
                return ChooseHardMove(board, moves, botSymbol, humanSymbol);

            if (difficulty == "Medium")
                return ChooseBestHeuristicMove(board, moves, botSymbol, humanSymbol);

            var candidates = EmptyCellsNearMoves(board, moves).ToList();
            if (candidates.Count == 0) candidates.Add((7, 7));
            return candidates[_random.Next(candidates.Count)];
        }

        private (int row, int col) FindFinishingMove(int[][] board, List<MoveRecordDto> moves, string symbol)
        {
            foreach (var (row, col) in CandidateMoves(board, moves, 2, 32))
            {
                board[row][col] = SymbolValue(symbol);
                var ok = WinChecker.HasWinner(board, row, col, symbol);
                board[row][col] = 0;
                if (ok) return (row, col);
            }
            return (-1, -1);
        }

        private IEnumerable<(int row, int col)> EmptyCellsNearMoves(int[][] board, List<MoveRecordDto> moves)
        {
            if (moves.Count == 0) { yield return (7, 7); yield break; }

            var seen = new HashSet<string>();
            foreach (var move in moves)
                for (var dr = -1; dr <= 1; dr++)
                    for (var dc = -1; dc <= 1; dc++)
                    {
                        var r = move.Row + dr;
                        var c = move.Col + dc;
                        var key = $"{r}:{c}";
                        if (r >= 0 && r < Board.Size && c >= 0 && c < Board.Size && board[r][c] == 0 && seen.Add(key))
                            yield return (r, c);
                    }
        }

        private (int row, int col) ChooseBestHeuristicMove(int[][] board, List<MoveRecordDto> moves, string botSymbol, string humanSymbol)
        {
            return CandidateMoves(board, moves, 2, 18)
                .OrderByDescending(m => ScoreCandidate(board, m.row, m.col, botSymbol, humanSymbol))
                .FirstOrDefault((-1, -1));
        }

        private (int row, int col) ChooseHardMove(int[][] board, List<MoveRecordDto> moves, string botSymbol, string humanSymbol)
        {
            var candidates = CandidateMoves(board, moves, 2, 14).ToList();
            if (candidates.Count == 0) return (7, 7);

            var botValue = SymbolValue(botSymbol);
            var bestMove = candidates[0];
            var bestScore = int.MinValue;

            foreach (var move in candidates)
            {
                board[move.row][move.col] = botValue;
                var score = WinChecker.HasWinner(board, move.row, move.col, botSymbol)
                    ? 50_000_000
                    : Minimax(board, moves, 3, false, botSymbol, humanSymbol, int.MinValue / 2, int.MaxValue / 2);
                board[move.row][move.col] = 0;

                if (score > bestScore) { bestScore = score; bestMove = move; }
            }
            return bestMove;
        }

        private int Minimax(int[][] board, List<MoveRecordDto> moves, int depth, bool maximizing,
                            string botSymbol, string humanSymbol, int alpha, int beta)
        {
            if (depth == 0 || board.All(row => row.All(c => c != 0)))
                return EvaluateBoard(board, botSymbol, humanSymbol);

            var symbol = maximizing ? botSymbol : humanSymbol;
            var value = SymbolValue(symbol);
            var candidates = CandidateMoves(board, moves, 2, depth >= 3 ? 12 : 10).ToList();
            if (candidates.Count == 0)
                return EvaluateBoard(board, botSymbol, humanSymbol);

            if (maximizing)
            {
                var best = int.MinValue / 2;
                foreach (var move in candidates)
                {
                    board[move.row][move.col] = value;
                    var score = WinChecker.HasWinner(board, move.row, move.col, symbol)
                        ? 40_000_000 + depth
                        : Minimax(board, moves, depth - 1, false, botSymbol, humanSymbol, alpha, beta);
                    board[move.row][move.col] = 0;

                    best = Math.Max(best, score);
                    alpha = Math.Max(alpha, best);
                    if (beta <= alpha) break;
                }
                return best;
            }
            else
            {
                var worst = int.MaxValue / 2;
                foreach (var move in candidates)
                {
                    board[move.row][move.col] = value;
                    var score = WinChecker.HasWinner(board, move.row, move.col, symbol)
                        ? -40_000_000 - depth
                        : Minimax(board, moves, depth - 1, true, botSymbol, humanSymbol, alpha, beta);
                    board[move.row][move.col] = 0;

                    worst = Math.Min(worst, score);
                    beta = Math.Min(beta, worst);
                    if (beta <= alpha) break;
                }
                return worst;
            }
        }

        private IEnumerable<(int row, int col)> CandidateMoves(int[][] board, List<MoveRecordDto> moves, int radius, int limit)
        {
            if (moves.Count == 0) return new[] { (7, 7) };

            var seen = new HashSet<string>();
            var result = new List<(int row, int col)>();
            foreach (var move in moves)
                for (var dr = -radius; dr <= radius; dr++)
                    for (var dc = -radius; dc <= radius; dc++)
                    {
                        var r = move.Row + dr;
                        var c = move.Col + dc;
                        var key = $"{r}:{c}";
                        if (r >= 0 && r < Board.Size && c >= 0 && c < Board.Size && board[r][c] == 0 && seen.Add(key))
                            result.Add((r, c));
                    }

            var botSymbol = "X";
            return result
                .OrderByDescending(m => ScoreCandidate(board, m.row, m.col, botSymbol, botSymbol == "X" ? "O" : "X"))
                .ThenBy(m => Math.Abs(m.row - 7) + Math.Abs(m.col - 7))
                .Take(limit)
                .ToList();
        }

        private int ScoreCandidate(int[][] board, int row, int col, string botSymbol, string humanSymbol)
        {
            var botValue = SymbolValue(botSymbol);
            var humanValue = SymbolValue(humanSymbol);

            board[row][col] = botValue;
            var attack = EvaluatePoint(board, row, col, botValue);
            board[row][col] = humanValue;
            var defense = EvaluatePoint(board, row, col, humanValue);
            board[row][col] = 0;

            return attack + defense * 2;
        }

        private int EvaluateBoard(int[][] board, string botSymbol, string humanSymbol)
        {
            return EvaluatePlayer(board, SymbolValue(botSymbol)) - EvaluatePlayer(board, SymbolValue(humanSymbol)) * 2;
        }

        private int EvaluatePlayer(int[][] board, int value)
        {
            var score = 0;
            for (var r = 0; r < Board.Size; r++)
                for (var c = 0; c < Board.Size; c++)
                    if (board[r][c] == value)
                        score += EvaluatePoint(board, r, c, value);
            return score;
        }

        private int EvaluatePoint(int[][] board, int row, int col, int value)
        {
            var score = 0;
            var directions = new[] { (0, 1), (1, 0), (1, 1), (1, -1) };
            foreach (var (dr, dc) in directions)
            {
                var forward = CountLine(board, row, col, dr, dc, value, out var openForward);
                var backward = CountLine(board, row, col, -dr, -dc, value, out var openBackward);
                var length = 1 + forward + backward;
                var openEnds = (openForward ? 1 : 0) + (openBackward ? 1 : 0);
                score += ScorePattern(length, openEnds);
            }
            return score;
        }

        private int CountLine(int[][] board, int row, int col, int dr, int dc, int value, out bool openEnd)
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
            openEnd = r >= 0 && r < Board.Size && c >= 0 && c < Board.Size && board[r][c] == 0;
            return count;
        }

        private static int ScorePattern(int length, int openEnds)
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

        private static int SymbolValue(string symbol) => symbol == "X" ? 1 : 2;
    }
}
