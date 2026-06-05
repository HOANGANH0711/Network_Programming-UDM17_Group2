using System;
using System.Collections.Generic;

namespace Shared.DTO
{
    public class InviteDto
    {
        public string FromPlayerId { get; set; } = "";
        public string FromPlayerName { get; set; } = "";
        public string ToPlayerId { get; set; } = "";
        public string ToPlayerName { get; set; } = "";
        public int TurnSeconds { get; set; } = 30;
        public string InviterSymbol { get; set; } = "X";
        public string InviteeSymbol => InviterSymbol == "X" ? "O" : "X";
    }

    public class InviteResponseDto
    {
        public string FromPlayerId { get; set; } = "";
        public string ToPlayerId { get; set; } = "";
        public bool Accepted { get; set; }
    }

    public class GameStartDto
    {
        public string RoomId { get; set; } = "";
        public string Player1Id { get; set; } = "";
        public string Player1Name { get; set; } = "";
        public string Player2Id { get; set; } = "";
        public string Player2Name { get; set; } = "";
        public int TurnSeconds { get; set; } = 30;
        public string XPlayerId { get; set; } = "";
        public string OPlayerId { get; set; } = "";
    }

    public class GameStateDto
    {
        public string GameID { get; set; } = "";
        public string PlayerXID { get; set; } = "";
        public string PlayerXName { get; set; } = "";
        public string PlayerOID { get; set; } = "";
        public string PlayerOName { get; set; } = "";
        public string CurrentTurnID { get; set; } = "";
        public string CurrentSymbol { get; set; } = "X";
        public string YourSymbol { get; set; } = "";
        public int[][] Board { get; set; } = Array.Empty<int[]>();
        public int TimeRemaining { get; set; }
        public int TurnSeconds { get; set; }
        public bool IsGameOver { get; set; }
        public string WinnerID { get; set; } = "";
        public string ResultText { get; set; } = "";
        public List<MoveRecordDto> Moves { get; set; } = new List<MoveRecordDto>();
        public bool IsBotGame { get; set; }
    }

    public class MoveRecordDto
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public string PlayerID { get; set; } = "";
        public string Symbol { get; set; } = "";
        public DateTime Time { get; set; } = DateTime.Now;
    }

    public class ChatDto
    {
        public string GameID { get; set; } = "";
        public string FromPlayerID { get; set; } = "";
        public string FromPlayerName { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime SentAt { get; set; } = DateTime.Now;
    }

    public class HistoryItemDto
    {
        public string GameID { get; set; } = "";
        public DateTime PlayedAt { get; set; } = DateTime.Now;
        public string OpponentName { get; set; } = "";
        public string Result { get; set; } = "";
        public string Mode { get; set; } = "";
        public List<MoveRecordDto> Moves { get; set; } = new List<MoveRecordDto>();
    }

    public class BotGameRequestDto
    {
        public string Difficulty { get; set; } = "Easy";
        public int TurnSeconds { get; set; } = 30;
        public string PlayerSymbol { get; set; } = "X";
    }
}
