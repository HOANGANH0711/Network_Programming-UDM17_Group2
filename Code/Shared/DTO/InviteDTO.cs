namespace Shared.DTO
{
    public class InviteDTO
    {
        public UserDTO Inviter { get; set; } = new UserDTO();
        public UserDTO Target { get; set; } = new UserDTO();
        public int TurnSeconds { get; set; } = 30;
        public bool InviterPlaysX { get; set; } = true;
    }
}
