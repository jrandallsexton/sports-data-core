using FluentAssertions;

using SportsData.Core.Infrastructure.DataSources.Espn;

using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.DataSources.Espn
{
    public class EspnUriMapperTests
    {
        // 1) Happy path: HTTP + querystring preserved
        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/teams/2673?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2673?lang=en&region=us")]
        public void TeamSeasonToFranchiseRef_Should_Map_To_Franchise_And_Preserve_Query_Http(
            string teamSeasonRef,
            string expectedFranchiseRef)
        {
            var input = new Uri(teamSeasonRef);
            var result = EspnUriMapper.TeamSeasonToFranchiseRef(input);
            result.Should().Be(new Uri(expectedFranchiseRef));
        }

        // 2) Same path but HTTPS: ensure scheme is preserved
        [Theory]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/teams/2673?lang=en&region=us",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2673?lang=en&region=us")]
        public void TeamSeasonToFranchiseRef_Should_Preserve_Scheme_Https(
            string teamSeasonRef,
            string expectedFranchiseRef)
        {
            var input = new Uri(teamSeasonRef);
            var result = EspnUriMapper.TeamSeasonToFranchiseRef(input);
            result.Should().Be(new Uri(expectedFranchiseRef));
        }

        // 3) Guards: unexpected shape should throw (no "teams/{id}" segment)
        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2673?lang=en&region=us")]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/teams/?lang=en&region=us")]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/athletes/123?lang=en&region=us")]
        public void TeamSeasonToFranchiseRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.TeamSeasonToFranchiseRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        // 4) Null guard
        [Fact]
        public void TeamSeasonToFranchiseRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.TeamSeasonToFranchiseRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
