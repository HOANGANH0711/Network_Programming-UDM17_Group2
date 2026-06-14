using Shared.DTO;

namespace Server.GameLogic
{
    public sealed class ActiveGame
    {
        public const string BotId = "BOT";

        public string GameID { get; } = Guid.NewGuid().ToString("N");
        public string PlayerXID { get; private set; } = "";
        public string PlayerXName { get; private set; } = "";
        public string PlayerOID { get; private set; } = "";
        public string PlayerOName { get; private set; } = "";
        public string CurrentTurnID { get; private set; } = "";
        public string CurrentSymbol { get; private set; } = "X";
        public int TimeRemaining { get; set; }
        public int TurnSeconds { get; private set; }
        public bool IsGameOver { get; set; }
        public string WinnerID { get; set; } = "";
        public string ResultText { get; set; } = "";
        public bool IsBotGame { get; private set; }
        public string BotDifficulty { get; private set; } = "Easy";
        public List<MoveRecordDto> Moves { get; } = new List<MoveRecordDto>();

        private readonly Board _board = new Board();
        private readonly BotEngine _bot = new BotEngine();
        private Timer? _timer;

        // Giữ property Board để các chỗ khác (ServerManager, DTO) không cần sửa
        public int[][] Board => _board.Cells;

        public static ActiveGame CreateOnline(string xId, string xName, string oId, string oName, int turnSeconds)
        {
            return new ActiveGame
            {
                PlayerXID = xId,
                PlayerXName = xName,
                PlayerOID = oId,
                PlayerOName = oName,
                CurrentTurnID = xId,
                TurnSeconds = Math.Max(15, turnSeconds),
                TimeRemaining = Math.Max(15, turnSeconds)
            };
        }

        public static ActiveGame CreateBot(string xId, string xName, string oId, string oName, int turnSeconds, string difficulty)
        {
            var game = CreateOnline(xId, xName, oId, oName, turnSeconds);
            game.IsBotGame = true;
            game.BotDifficulty = difficulty;
            return game;
        }

        public void StartTimer(Action tick)
        {
            _timer = new Timer(_ => tick(), null, 1000, 1000);
        }

        public void StopTimer()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public string SymbolOf(string playerId)
        {
            if (playerId == PlayerXID) return "X";
            if (playerId == PlayerOID) return "O";
            return "";
        }

        public string OpponentOf(string playerId) => playerId == PlayerXID ? PlayerOID : PlayerXID;

        public bool PlaceMove(string playerId, string symbol, int row, int col)
        {
            if (!_board.PlaceMove(symbol, row, col))
                return false;

            Moves.Add(new MoveRecordDto { Row = row, Col = col, PlayerID = playerId, Symbol = symbol });
            TimeRemaining = TurnSeconds;
            return true;
        }

        public void SwitchTurn()
        {
            CurrentTurnID = CurrentTurnID == PlayerXID ? PlayerOID : PlayerXID;
            CurrentSymbol = CurrentTurnID == PlayerXID ? "X" : "O";
            TimeRemaining = TurnSeconds;
        }

        public bool HasWinner(int row, int col, string symbol)
            => WinChecker.HasWinner(_board.Cells, row, col, symbol);

        public bool IsBoardFull()
            => _board.IsFull();

        public (int row, int col) ChooseBotMove()
            => _bot.ChooseMove(_board.Cells, Moves, SymbolOf(BotId), BotDifficulty);

        public GameStateDto ToDto(string viewerId)
        {
            return new GameStateDto
            {
                GameID = GameID,
                PlayerXID = PlayerXID,
                PlayerXName = PlayerXName,
                PlayerOID = PlayerOID,
                PlayerOName = PlayerOName,
                CurrentTurnID = CurrentTurnID,
                CurrentSymbol = CurrentSymbol,
                YourSymbol = SymbolOf(viewerId),
                Board = _board.ToArray(),
                TimeRemaining = TimeRemaining,
                TurnSeconds = TurnSeconds,
                IsGameOver = IsGameOver,
                WinnerID = WinnerID,
                ResultText = ResultText,
                Moves = Moves.ToList(),
                IsBotGame = IsBotGame
            };
        }

        public HistoryItemDto ToHistory(string playerId, string opponentName)
        {
            var result = WinnerID == "" ? "Hoa" : WinnerID == playerId ? "Thang" : "Thua";
            return new HistoryItemDto
            {
                GameID = $"{playerId}-{GameID}",
                PlayedAt = DateTime.Now,
                OpponentName = opponentName,
                Result = result,
                Mode = IsBotGame ? $"Bot {BotDifficulty}" : "Online",
                Moves = Moves.ToList()
            };
        }
    }
}