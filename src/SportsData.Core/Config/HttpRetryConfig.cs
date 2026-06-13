namespace SportsData.Core.Config
{
    public class HttpRetryConfig
    {
        public int RetryCount { get; set; } = 3;

        public int BaseDelayMs { get; set; } = 200;
    }
}
