namespace StatusImageCard.Models
{
    public class StatusImageModel
    {
        public required byte[] PngBytes { get; set; }
        public required string CurrentlyShowing { get; set; }
        public required string[] OnlineServers { get; set; }
    }
}
