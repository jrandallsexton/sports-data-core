using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Common.Routing;

using Xunit;

namespace SportsData.Core.Tests.Unit.Common.Routing
{
    public class RoutingKeyGeneratorTests
    {
        private readonly RoutingKeyGenerator _generator = new();

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl",
            "espn.sports.football.leagues.nfl", SourceDataProvider.Espn)]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football",
            "espn.sports.football.leagues.college-football", SourceDataProvider.Espn)]
        [InlineData("https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2024/types",
            "espn.sports.football.leagues.nfl.seasons.2024.types", SourceDataProvider.Espn)]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2024/types/3",
            "espn.sports.football.leagues.nfl.seasons.2024.types.3", SourceDataProvider.Espn)]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2024/types/3?lang=en&region=us",
            "espn.sports.football.leagues.nfl.seasons.2024.types.3", SourceDataProvider.Espn)]
        [InlineData("https://sports.core.api.espn.com/v2/", "", SourceDataProvider.Espn)]
        public void Generate_ShouldReturnExpectedRoutingKey(string inputUrl, string expectedKey, SourceDataProvider provider)
        {
            var result = _generator.Generate(provider, inputUrl);

            result.Should().Be(expectedKey);
        }
    }
}