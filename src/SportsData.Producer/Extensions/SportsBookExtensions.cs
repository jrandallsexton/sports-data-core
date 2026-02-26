using SportsData.Producer.Enums;

namespace SportsData.Producer.Extensions
{
    public static class SportsBookExtensions
    {
        public static string ToProviderId(this SportsBook sportsBook)
        {
            return ((int)sportsBook).ToString();
        }
    }
}
