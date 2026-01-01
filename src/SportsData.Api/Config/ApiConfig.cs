namespace SportsData.Api.Config
{
    public class ApiConfig
    {
        public List<string> SupportedModes { get; set; } = new();

        public required string BaseUrl { get; set; }

        public required Guid UserIdSystem { get; set; }
    }
}
