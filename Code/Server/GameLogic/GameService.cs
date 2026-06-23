namespace Server.GameLogic
{
    public class GameService
    {
        private readonly Random _random = new Random();

        public (int row, int col) ChooseBotMove(Board board, List<Move> moves, string botSymbol, string difficulty)
        {
            var humanSymbol = botSymbol == "X" ? "O" : "X";

            if (moves.Count == 0)
                return (7, 7);

            if (difficulty != "Easy")
            {
                var win = FindFinishingMove(board, moves, botSymbol);
                if (win.row >= 0) return win;
            }

            if (difficulty != "Easy")
            {
                var block = FindFinishingMove(board, moves, humanSymbol);
                if (block.row >= 0) return block;
            }

            return difficulty switch
            {
                "Hard" => ChooseHardMove(board, moves, botSymbol, humanSymbol),
                "Medium" => ChooseBestHeuristicMove(board, moves, botSymbol, humanSymbol),
                _ => ChooseEasyMove(board, moves)
            };
        }

        private (int row, int col) ChooseEasyMove(Board board, List<Move> moves)
        {
            var candidates = EmptyCellsNear(board, moves, radius: 1).ToList();
            if (candidates.Count == 0) candidates.Add((7, 7));
            return candidates[_random.Next(candidates.Count)];
        }

        private (int row, int col) ChooseBestHeuristicMove(Board board, List<Move> moves, string botSymbol, string humanSymbol)
        {
            return CandidateMoves(board, moves, radius: 2, limit: 18, botSymbol, humanSymbol)
                .OrderByDescending(m => ScoreCandidate(board, m.row, m.col, botSymbol, humanSymbol))
                .FirstOrDefault((-1, -1));
        }

        private (int row, int col) ChooseHardMove(Board board, List<Move> moves, string botSymbol, string humanSymbol)
        {
            var candidates = CandidateMoves(board, moves, radius: 2, limit: 14, botSymbol, humanSymbol).ToList();
            if (candidates.Count == 0) return (7, 7);

            var botValue = SymbolValue(botSymbol);
            var bestMove = candidates[0];
            var bestScore = int.MinValue;

            foreach (var move in candidates)
            {
                board.SetRaw(move.row, move.col, botValue);
                var score = WinChecker.HasWinner(board, move.row, move.col, botSymbol)
                    ? 50_000_000
                    : Minimax(board, depth: 3, maximizing: false, botSymbol, humanSymbol, int.MinValue / 2, int.MaxValue / 2);
                board.SetRaw(move.row, move.col, 0);

                if (score > bestScore) { bestScore = score; bestMove = move; }
            }

            return bestMove;
        }

        private int Minimax(Board board, int depth, bool maximizing, string botSymbol, string humanSymbol, int alpha, int beta)
        {
            if (depth == 0 || board.IsFull())
                return EvaluateBoard(board, botSymbol, humanSymbol);
            var symbol = maximizing ? botSymbol : humanSymbol;
            var value = SymbolValue(symbol);
            var moves = CandidateMoves(board, new List<Move>(), radius: 2, limit: depth >= 3 ? 12 : 10, botSymbol, humanSymbol).ToList();

            if (moves.Count == 0)
                return EvaluateBoard(board, botSymbol, humanSymbol);

            if (maximizing)
            {
                var best = int.MinValue / 2;
                foreach (var move in moves)
                {
                    board.SetRaw(move.row, move.col, value);
                    var score = WinChecker.HasWinner(board, move.row, move.col, symbol)
                        ? 40_000_000 + depth
                        : Minimax(board, depth - 1, false, botSymbol, humanSymbol, alpha, beta);
                    board.SetRaw(move.row, move.col, 0);
                    best = Math.Max(best, score);
                    alpha = Math.Max(alpha, best);
                    if (beta <= alpha) break;
                }
                return best;
            }
            else
            {
                var worst = int.MaxValue / 2;
                foreach (var move in moves)
                {
                    board.SetRaw(move.row, move.col, value);
                    var score = WinChecker.HasWinner(board, move.row, move.col, symbol)
                        ? -40_000_000 - depth
                        : Minimax(board, depth - 1, true, botSymbol, humanSymbol, alpha, beta);
                    board.SetRaw(move.row, move.col, 0);
                    worst = Math.Min(worst, score);
                    beta = Math.Min(beta, worst);
                    if (beta <= alpha) break;
                }
                return worst;
            }
        }

        private (int row, int col) FindFinishingMove(Board board, List<Move> moves, string symbol)
        {
            foreach (var (row, col) in CandidateMoves(board, moves, radius: 2, limit: 32, symbol, symbol == "X" ? "O" : "X"))
            {
                board.SetRaw(row, col, SymbolValue(symbol));
                var ok = WinChecker.HasWinner(board, row, col, symbol);
                board.SetRaw(row, col, 0);
                if (ok) return (row, col);
            }
            return (-1, -1);
        }

        private IEnumerable<(int row, int col)> EmptyCellsNear(Board board, List<Move> moves, int radius)
        {
            if (moves.Count == 0) { yield return (7, 7); yield break; }
            var seen = new HashSet<string>();
            foreach (var move in moves)
                for (var dr = -radius; dr <= radius; dr++)
                    for (var dc = -radius; dc <= radius; dc++)
                    {
                        var r = move.Row + dr;
                        var c = move.Col + dc;
                        var key = $"{r}:{c}";
                        if (r >= 0 && r < Board.Size && c >= 0 && c < Board.Size
                            && board.GetRaw(r, c) == 0 && seen.Add(key))
                            yield return (r, c);
                    }
        }

        private IEnumerable<(int row, int col)> CandidateMoves(Board board, List<Move> moves, int radius, int limit, string botSymbol, string humanSymbol)
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
                        if (r >= 0 && r < Board.Size && c >= 0 && c < Board.Size
                            && board.GetRaw(r, c) == 0 && seen.Add(key))
                            result.Add((r, c));
                    }

            return result
                .OrderByDescending(m => ScoreCandidate(board, m.row, m.col, botSymbol, humanSymbol))
                .ThenBy(m => Math.Abs(m.row - 7) + Math.Abs(m.col - 7))
                .Take(limit);
        }

        private int ScoreCandidate(Board board, int row, int col, string botSymbol, string humanSymbol)
        {
            var botValue = SymbolValue(botSymbol);
            var humanValue = SymbolValue(humanSymbol);

            board.SetRaw(row, col, botValue);
            var attack = WinChecker.EvaluatePoint(board, row, col, botValue);
            board.SetRaw(row, col, humanValue);
            var defense = WinChecker.EvaluatePoint(board, row, col, humanValue);
            board.SetRaw(row, col, 0);

            return attack + defense * 2;
        }

        private int EvaluateBoard(Board board, string botSymbol, string humanSymbol) =>
            EvaluatePlayer(board, SymbolValue(botSymbol))
            - EvaluatePlayer(board, SymbolValue(humanSymbol)) * 2;

        private int EvaluatePlayer(Board board, int value)
        {
            var score = 0;
            for (var r = 0; r < Board.Size; r++)
                for (var c = 0; c < Board.Size; c++)
                    if (board.GetRaw(r, c) == value)
                        score += WinChecker.EvaluatePoint(board, r, c, value);
            return score;
        }

        private static int SymbolValue(string symbol) => symbol == "X" ? 1 : 2;
    }
}
