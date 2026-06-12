namespace Server.GameLogic
{
    public class Move
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public string PlayerID { get; set; } = "";
        public string Symbol { get; set; } = "";
    }
}