namespace Shared.DTO
{
    public class LobbyUpdateDto
    {
        public List<PlayerInfoDto> OnlinePlayers { get; set; } = new();
    }

    public class PlayerInfoDto
    {
        public string PlayerId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public string Status { get; set; } = "";
    }
}