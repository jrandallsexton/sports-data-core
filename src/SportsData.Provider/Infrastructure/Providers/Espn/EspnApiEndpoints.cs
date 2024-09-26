namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    public static class EspnApiEndpoints
    {
        public static string Awards(int franchiseId) => $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/{franchiseId}/awards?lang=en";

        public static string Franchise(int franchiseId) => $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/{franchiseId}?lang=en";

        public static string Franchises = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises?lang=en&limit=999";

        public static string Team(int fourDigitYear, int teamId) => "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/{fourDigitYear}/teams/{teamId}?lang=en";

        public static string TeamInformation(int teamId) => "http://site.api.espn.com/apis/site/v2/sports/football/college-football/teams/{teamId}";

        public static string Teams(int fourDigitYear) => $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/{fourDigitYear}/teams?lang=en&limit=900";

        public static string Venues = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues?lang=en&limit=999";

        public static string Venue(int venueId) => $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues/{venueId}?lang=en";
    }
}
