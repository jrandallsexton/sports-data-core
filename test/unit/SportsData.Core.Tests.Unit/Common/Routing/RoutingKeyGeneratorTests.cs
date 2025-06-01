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
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues/123",
            "espn.sports.football.leagues.college-football.venues", SourceDataProvider.Espn)]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/85/record",
            "espn.sports.football.leagues.college-football.seasons.teams.record", SourceDataProvider.Espn)]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/1234/competitions/5678/plays/12",
            "espn.sports.football.leagues.college-football.events.competitions.plays", SourceDataProvider.Espn)]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/types/3/teams/45/ats",
            "espn.sports.football.leagues.college-football.seasons.types.teams.ats", SourceDataProvider.Espn)]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2/awards",
            "espn.sports.football.leagues.college-football.franchises.awards", SourceDataProvider.Espn)]
        [InlineData("http://sports.core.api.espn.com/v2/", "", SourceDataProvider.Espn)]
        public void Generate_ShouldReturnExpectedRoutingKey(string inputUrl, string expectedKey, SourceDataProvider provider)
        {
            var result = _generator.Generate(provider, inputUrl);
            result.Should().Be(expectedKey);
        }

    }
}