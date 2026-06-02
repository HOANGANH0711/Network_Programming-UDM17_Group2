using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.GameLogic
{
    public enum GameStatus { Waiting, Playing, Finished }

    public class GameService
    {
        private Board _board;
        public CellState CurrentPlayer { get; private set; }
        public GameStatus Status { get; private set; }
        public CellState? Winner { get; private set; }

        public GameService()
        {
            _board = new Board();
            CurrentPlayer = CellState.X;  // X di truoc
            Status = GameStatus.Waiting;
            Winner = null;
        }

        public void StartGame()
        {
            _board.Reset();
            CurrentPlayer = CellState.X;
            Status = GameStatus.Playing;
            Winner = null;
        }

        public string MakeMove(Move move)
        {
            if (Status != GameStatus.Playing)
                return "Game chưa bắt đầu hoặc đã kết thúc!";

            if (move.Player != CurrentPlayer)
                return "Không phải lượt của bạn!";

            if (!_board.PlaceMove(move.Row, move.Col, move.Player))
                return "Nước đi không hợp lệ!";

            if (WinChecker.CheckWin(_board, move.Row, move.Col, move.Player))
            {
                Status = GameStatus.Finished;
                Winner = move.Player;
                return $"WIN:{move.Player}";
            }
            if (_board.IsFull())
            {
                Status = GameStatus.Finished;
                return "DRAW";
            }
            // Chuyen luot
            CurrentPlayer = (CurrentPlayer == CellState.X)
                            ? CellState.O : CellState.X;
            return "OK";
        }
    }
}
