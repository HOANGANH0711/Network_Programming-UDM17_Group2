using Shared.Enums;

namespace Shared.Models
{
    public class Player
    {
        public string PlayerId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public PlayerStatus Status { get; set; }

        public Player(string playerId, string playerName)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            Status = PlayerStatus.Online;
        }
    }
}