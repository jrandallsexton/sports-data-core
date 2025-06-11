using System.Collections;
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
        [ClassData(typeof(RoutingKeyTestData))]
        public void Generate_ShouldReturnExpectedRoutingKey(Uri inputUri, string expectedKey, SourceDataProvider provider)
        {
            var result = _generator.Generate(provider, inputUri);
            result.Should().Be(expectedKey);
        }

    }

    public class RoutingKeyTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[]
            {
                new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues/123"),
                "espn.v2.sports.football.leagues.college-football.venues",
                SourceDataProvider.Espn
            };

            yield return new object[]
            {
                new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/85/record"),
                "espn.v2.sports.football.leagues.college-football.seasons.teams.record",
                SourceDataProvider.Espn
            };

            yield return new object[]
            {
                new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/1234/competitions/5678/plays/12"),
                "espn.v2.sports.football.leagues.college-football.events.competitions.plays",
                SourceDataProvider.Espn
            };

            yield return new object[]
            {
                new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/types/3/teams/45/ats"),
                "espn.v2.sports.football.leagues.college-football.seasons.types.teams.ats",
                SourceDataProvider.Espn
            };

            yield return new object[]
            {
                new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2/awards"),
                "espn.v2.sports.football.leagues.college-football.franchises.awards",
                SourceDataProvider.Espn
            };

            yield return new object[]
            {
                new Uri("http://sports.core.api.espn.com/v2/"),
                "",
                SourceDataProvider.Espn
            };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}