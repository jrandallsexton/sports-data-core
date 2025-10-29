namespace SportsData.Core.Infrastructure.Clients.YouTube
{
    public class YouTubeClientConfig
    {
        public string ApiKey { get; set; } = null!;
        public string BaseUrl { get; set; } = "https://youtube.googleapis.com/youtube/v3";
        public string DefaultChannelId { get; set; } = "UCzRWWsFjqHk1an4OnVPsl9g";
    }
}
