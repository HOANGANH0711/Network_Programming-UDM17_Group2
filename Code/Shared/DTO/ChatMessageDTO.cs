using System;

namespace Shared.DTO
{
    public class ChatMessageDTO
    {
        public string GameID { get; set; } = string.Empty;
        public string SenderID { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.Now;
    }
}
