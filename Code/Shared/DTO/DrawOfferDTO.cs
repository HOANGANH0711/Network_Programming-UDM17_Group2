namespace Shared.DTO
{
    public class DrawOfferDTO
    {
        public string GameID { get; set; } = string.Empty;
        public string FromPlayerID { get; set; } = string.Empty;
        public string ToPlayerID { get; set; } = string.Empty;
        public bool Accepted { get; set; }
    }
}
