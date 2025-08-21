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

        // 5) Happy path: AthleteSeason to Athlete with querystring and HTTPS preserved
        [Theory]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/4426333?lang=en&region=us",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/4426333?lang=en&region=us")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/-6952?lang=en&region=us",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/-6952?lang=en&region=us")]
        public void AthleteSeasonToAthleteRef_Should_Map_To_Athlete_And_Preserve_Query_Https(
            string athleteSeasonRef,
            string expectedAthleteRef)
        {
            var input = new Uri(athleteSeasonRef);
            var result = EspnUriMapper.AthleteSeasonToAthleteRef(input);
            result.Should().Be(new Uri(expectedAthleteRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2/weeks/1?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2?lang=en&region=us")]
        public void SeasonTypeWeekToSeasonType_Should_Map_To_SeasonType_And_Preserve_Query(
            string weekRef,
            string expectedSeasonTypeRef)
        {
            var input = new Uri(weekRef);
            var result = EspnUriMapper.SeasonTypeWeekToSeasonType(input);
            result.Should().Be(new Uri(expectedSeasonTypeRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025?lang=en&region=us")]
        public void SeasonTypeToSeason_Should_Map_To_Season_And_Preserve_Query(
            string seasonTypeRef,
            string expectedSeasonRef)
        {
            var input = new Uri(seasonTypeRef);
            var result = EspnUriMapper.SeasonTypeToSeason(input);
            result.Should().Be(new Uri(expectedSeasonRef));
        }

    }
}
