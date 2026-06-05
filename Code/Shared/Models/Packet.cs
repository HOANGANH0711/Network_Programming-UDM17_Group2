using Shared.Enums;

namespace Shared.Models
{
    public class Packet
    {
        public CommandType Command { get; set; }
        public string Data { get; set; } = string.Empty;
        public string SenderID { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        public Packet()
        {
            Timestamp = DateTime.Now;
        }
    }
}
