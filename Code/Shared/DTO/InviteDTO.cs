namespace Shared.DTO
{
    public class InviteDto
    {
        public string FromPlayerId { get; set; } = "";
        public string FromPlayerName { get; set; } = "";
        public string ToPlayerId { get; set; } = "";
    }

    public class GameStartDto
    {
        public string RoomId { get; set; } = "";
        public string Player1Id { get; set; } = "";
        public string Player1Name { get; set; } = "";
        public string Player2Id { get; set; } = "";
        public string Player2Name { get; set; } = "";
    }

    public class NotifyDto
    {
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
    }
}