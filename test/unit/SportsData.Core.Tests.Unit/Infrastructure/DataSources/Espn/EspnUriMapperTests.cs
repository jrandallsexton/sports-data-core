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

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2009/awards/3",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/awards/3")]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/awards/3?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/awards/3?lang=en&region=us")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/awards/1?lang=en&region=us",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/awards/1?lang=en&region=us")]
        public void SeasonAwardToAwardRef_Should_Map_To_Award_And_Preserve_Query(
            string seasonAwardRef,
            string expectedAwardRef)
        {
            var input = new Uri(seasonAwardRef);
            var result = EspnUriMapper.SeasonAwardToAwardRef(input);
            result.Should().Be(new Uri(expectedAwardRef));
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/awards/3")] // missing seasons segment
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/awards/")] // missing award ID
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/teams/123")] // wrong resource type
        public void SeasonAwardToAwardRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.SeasonAwardToAwardRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void SeasonAwardToAwardRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.SeasonAwardToAwardRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/status")]
        public void CompetitionRefToCompetitionStatusRef_Should_Append_Status_Correctly(
            string competitionRef,
            string expectedStatusRef)
        {
            var input = new Uri(competitionRef);
            var result = EspnUriMapper.CompetitionRefToCompetitionStatusRef(input);
            result.Should().Be(new Uri(expectedStatusRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2/weeks/2/rankings/1",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/rankings/1")]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1/rankings/2?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/rankings/2")]
        public void SeasonPollWeekRefToSeasonPollRef_Should_Remove_Type_And_Week_Levels(
            string seasonPollWeekRef,
            string expectedSeasonPollRef)
        {
            var input = new Uri(seasonPollWeekRef);
            var result = EspnUriMapper.SeasonPollWeekRefToSeasonPollRef(input);
            result.Should().Be(new Uri(expectedSeasonPollRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752699/competitions/401752699/leaders?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752699/competitions/401752699")]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/leaders",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334")]
        public void CompetitionLeadersRefToCompetitionRef_Should_Trim_To_CompetitionUri(
            string inputRef,
            string expectedRef)
        {
            var input = new Uri(inputRef);
            var expected = new Uri(expectedRef);

            var result = EspnUriMapper.CompetitionLeadersRefToCompetitionRef(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(
            // with query + deep segments
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401773615/competitions/401773615/competitors/2231/linescores/1/3?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401773615/competitions/401773615")]
        [InlineData(
            // no query
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401700000/competitions/401700000/competitors/1/linescores/2",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401700000/competitions/401700000")]
        [InlineData(
            // trailing slash after id
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401800000/competitions/401800000/competitors/2/linescores/",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401800000/competitions/401800000")]
        [InlineData(
            // mixed casing (method should be case-insensitive on segment match)
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401900000/Competitions/401900000/Competitors/5/LineScores/10",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401900000/competitions/401900000")]
        public void CompetitionLineScoreRefToCompetitionRef_Should_Trim_To_CompetitionUri(
            string inputRef,
            string expectedRef)
        {
            var input = new Uri(inputRef);
            var expected = new Uri(expectedRef);

            var result = EspnUriMapper.CompetitionLineScoreRefToCompetitionRef(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(
            // standard competition ref
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401773615/competitions/401773615",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401773615")]
        [InlineData(
            // mixed casing on segments
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/Events/401700000/Competitions/401700000",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401700000")]
        [InlineData(
            // trailing slash + query
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401800000/competitions/401800000/?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401800000")]
        [InlineData(
            // extra segments after competition id (should still trim back to event)
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401900000/competitions/401900000/boxscore/team",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401900000")]
        public void CompetitionRefToContestRef_Should_Trim_To_EventUri(
            string inputRef,
            string expectedRef)
        {
            var input = new Uri(inputRef);
            var expected = new Uri(expectedRef);

            var result = EspnUriMapper.CompetitionRefToContestRef(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(
            // with query + deep linescores path
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401773463/competitions/401773463/competitors/125/linescores/1/4?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401773463/competitions/401773463/competitors/125")]
        [InlineData(
            // no query, extra depth
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401700000/competitions/401700000/competitors/7/linescores/2/9",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401700000/competitions/401700000/competitors/7")]
        [InlineData(
            // trailing slash after competitor id
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401800000/competitions/401800000/competitors/33/linescores/",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401800000/competitions/401800000/competitors/33")]
        [InlineData(
            // mixed casing on segments (method normalizes segment names)
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/Events/401900000/Competitions/401900000/Competitors/125/LineScores/10",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401900000/competitions/401900000/competitors/125")]
        public void CompetitionLineScoreRefToCompetitionCompetitorRef_Should_Trim_To_CompetitorUri(
            string inputRef,
            string expectedRef)
        {
            var input = new Uri(inputRef);
            var expected = new Uri(expectedRef);

            var result = EspnUriMapper.CompetitionLineScoreRefToCompetitionCompetitorRef(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(
            // standard with query
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752710/competitions/401752710/competitors/99?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752710/competitions/401752710")]
        [InlineData(
            // no query
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401700000/competitions/401700000/competitors/12",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401700000/competitions/401700000")]
        [InlineData(
            // mixed casing on segments
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/Events/401800000/Competitions/401800000/Competitors/7",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401800000/competitions/401800000")]
        [InlineData(
            // trailing slash + extra segments (ignored)
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401900000/competitions/401900000/competitors/5/stats/",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401900000/competitions/401900000")]
        public void CompetitionCompetitorRefToCompetitionRef_Should_Trim_To_CompetitionUri(
            string inputRef,
            string expectedRef)
        {
            var input = new Uri(inputRef);
            var expected = new Uri(expectedRef);

            var result = EspnUriMapper.CompetitionCompetitorRefToCompetitionRef(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(
            // standard with query (your example)
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401767736/competitions/401767736/competitors/171/scores/1?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401767736/competitions/401767736/competitors/171")]
        [InlineData(
            // no query
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401767736/competitions/401767736/competitors/171/scores/1",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401767736/competitions/401767736/competitors/171")]
        [InlineData(
            // mixed casing on segments
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/Events/401767736/Competitions/401767736/Competitors/171/Scores/1",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401767736/competitions/401767736/competitors/171")]
        [InlineData(
            // trailing slash + extra segments (ignored)
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401767736/competitions/401767736/competitors/171/scores/1/stats/",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401767736/competitions/401767736/competitors/171")]
        public void CompetitionCompetitorScoreRefToCompetitionCompetitorRef_Should_Trim_To_CompetitionCompetitorUri(
            string inputRef,
            string expectedRef)
        {
            var input = new Uri(inputRef);
            var expected = new Uri(expectedRef);

            var result = EspnUriMapper.CompetitionCompetitorScoreRefToCompetitionCompetitorRef(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(
            // with query string
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/competitors/2640/statistics/0?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/competitors/2640")]
        [InlineData(
            // without query string
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/competitors/2640/statistics/0",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/competitors/2640")]
        [InlineData(
            // mixed casing on segments
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/Events/401540172/Competitions/401540172/Competitors/2640/Statistics/0",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/competitors/2640")]
        [InlineData(
            // trailing slash + extra segments (ignored)
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/competitors/2640/statistics/0/details/",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/competitors/2640")]
        public void CompetitionCompetitorStatisticsRefToCompetitionCompetitorRef_Should_Trim_To_CompetitionCompetitorUri(
            string inputRef,
            string expectedRef)
        {
            var input = new Uri(inputRef);
            var expected = new Uri(expectedRef);

            var result = EspnUriMapper.CompetitionCompetitorStatisticsRefToCompetitionCompetitorRef(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/statistics/0")] // missing competitors segment
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/competitors/")] // missing competitor ID
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/teams/2640/statistics/0")] // wrong segment (teams instead of competitors)
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2673")] // completely wrong shape
        public void CompetitionCompetitorStatisticsRefToCompetitionCompetitorRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.CompetitionCompetitorStatisticsRefToCompetitionCompetitorRef(input);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CompetitionCompetitorStatisticsRefToCompetitionCompetitorRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CompetitionCompetitorStatisticsRefToCompetitionCompetitorRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
