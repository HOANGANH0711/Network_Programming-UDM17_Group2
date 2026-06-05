namespace Shared.DTO
{
    public class TimerUpdateDTO
    {
        public string GameID { get; set; } = string.Empty;
        public string CurrentTurnID { get; set; } = string.Empty;
        public int RemainingSeconds { get; set; }
    }
}
