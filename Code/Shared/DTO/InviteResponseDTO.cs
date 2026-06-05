namespace Shared.DTO
{
    public class InviteResponseDTO
    {
        public InviteDTO Invite { get; set; } = new InviteDTO();
        public bool Accepted { get; set; }
    }
}
