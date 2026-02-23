using FluentAssertions;

using SportsData.Core.Infrastructure.DataSources.Espn;

using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.DataSources.Espn
{
    public class EspnUriMapperTests
    {
        // 1) Happy path: HTTP with query string stripped
        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/teams/2673?lang=en&region=us",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2673")]
        public void TeamSeasonToFranchiseRef_Should_Map_To_Franchise_Without_Query_Http(
            string teamSeasonRef,
            string expectedFranchiseRef)
        {
            var input = new Uri(teamSeasonRef);
            var result = EspnUriMapper.TeamSeasonToFranchiseRef(input);
            result.Should().Be(new Uri(expectedFranchiseRef));
        }

        // 2) Same path but HTTPS: ensure scheme is preserved but query is stripped
        [Theory]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/teams/2673?lang=en&region=us",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2673")]
        public void TeamSeasonToFranchiseRef_Should_Strip_Query_But_Preserve_Scheme_Https(
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

        // 5) Happy path: AthleteSeason to Athlete with query string stripped
        [Theory]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/4426333?lang=en&region=us",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/4426333")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/-6952?lang=en&region=us",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/-6952")]
        public void AthleteSeasonToAthleteRef_Should_Map_To_Athlete_Without_Query(
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
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2")]
        public void SeasonTypeWeekToSeasonType_Should_Map_To_SeasonType_Without_Query(
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
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025")]
        public void SeasonTypeToSeason_Should_Map_To_Season_Without_Query(
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
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/awards/3")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/awards/1?lang=en&region=us",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/awards/1")]
        public void SeasonAwardToAwardRef_Should_Map_To_Award_Without_Query(
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

        #region TeamSeason Child Resource Mappers

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395/statistics/0",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/2640/statistics/1?lang=en",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/2640")]
        public void TeamSeasonStatisticsRefToTeamSeasonRef_Should_Map_To_TeamSeason_Without_Query(
            string statisticsRef,
            string expectedTeamSeasonRef)
        {
            var input = new Uri(statisticsRef);
            var result = EspnUriMapper.TeamSeasonStatisticsRefToTeamSeasonRef(input);
            result.Should().Be(new Uri(expectedTeamSeasonRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395/leaders",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/2640/leaders?lang=en&region=us",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/2640")]
        public void TeamSeasonLeadersRefToTeamSeasonRef_Should_Map_To_TeamSeason_Without_Query(
            string leadersRef,
            string expectedTeamSeasonRef)
        {
            var input = new Uri(leadersRef);
            var result = EspnUriMapper.TeamSeasonLeadersRefToTeamSeasonRef(input);
            result.Should().Be(new Uri(expectedTeamSeasonRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395/rank",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395")]
        public void TeamSeasonRankRefToTeamSeasonRef_Should_Map_To_TeamSeason_Without_Query(
            string rankRef,
            string expectedTeamSeasonRef)
        {
            var input = new Uri(rankRef);
            var result = EspnUriMapper.TeamSeasonRankRefToTeamSeasonRef(input);
            result.Should().Be(new Uri(expectedTeamSeasonRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395/record",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395")]
        public void TeamSeasonRecordRefToTeamSeasonRef_Should_Map_To_TeamSeason_Without_Query(
            string recordRef,
            string expectedTeamSeasonRef)
        {
            var input = new Uri(recordRef);
            var result = EspnUriMapper.TeamSeasonRecordRefToTeamSeasonRef(input);
            result.Should().Be(new Uri(expectedTeamSeasonRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395/ats",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395")]
        public void TeamSeasonRecordAtsRefToTeamSeasonRef_Should_Map_To_TeamSeason_Without_Query(
            string recordAtsRef,
            string expectedTeamSeasonRef)
        {
            var input = new Uri(recordAtsRef);
            var result = EspnUriMapper.TeamSeasonRecordAtsRefToTeamSeasonRef(input);
            result.Should().Be(new Uri(expectedTeamSeasonRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395/projection",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395")]
        public void TeamSeasonProjectionRefToTeamSeasonRef_Should_Map_To_TeamSeason_Without_Query(
            string projectionRef,
            string expectedTeamSeasonRef)
        {
            var input = new Uri(projectionRef);
            var result = EspnUriMapper.TeamSeasonProjectionRefToTeamSeasonRef(input);
            result.Should().Be(new Uri(expectedTeamSeasonRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395/awards/123",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395")]
        public void TeamSeasonAwardRefToTeamSeasonRef_Should_Map_To_TeamSeason_Without_Query(
            string awardRef,
            string expectedTeamSeasonRef)
        {
            var input = new Uri(awardRef);
            var result = EspnUriMapper.TeamSeasonAwardRefToTeamSeasonRef(input);
            result.Should().Be(new Uri(expectedTeamSeasonRef));
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/395/statistics/0")] // no seasons segment
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/123/statistics/0")] // wrong resource (athletes)
        public void TeamSeasonStatisticsRefToTeamSeasonRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.TeamSeasonStatisticsRefToTeamSeasonRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void TeamSeasonStatisticsRefToTeamSeasonRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.TeamSeasonStatisticsRefToTeamSeasonRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/395/leaders")] // no seasons segment
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/123/leaders")] // wrong resource (athletes)
        public void TeamSeasonLeadersRefToTeamSeasonRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.TeamSeasonLeadersRefToTeamSeasonRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void TeamSeasonLeadersRefToTeamSeasonRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.TeamSeasonLeadersRefToTeamSeasonRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/395/rank")] // no seasons segment
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/123/rank")] // wrong resource (athletes)
        public void TeamSeasonRankRefToTeamSeasonRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.TeamSeasonRankRefToTeamSeasonRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void TeamSeasonRankRefToTeamSeasonRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.TeamSeasonRankRefToTeamSeasonRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/395/record")] // no seasons segment
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/123/record")] // wrong resource (athletes)
        public void TeamSeasonRecordRefToTeamSeasonRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.TeamSeasonRecordRefToTeamSeasonRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void TeamSeasonRecordRefToTeamSeasonRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.TeamSeasonRecordRefToTeamSeasonRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/395/ats")] // no seasons segment
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/123/ats")] // wrong resource (athletes)
        public void TeamSeasonRecordAtsRefToTeamSeasonRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.TeamSeasonRecordAtsRefToTeamSeasonRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void TeamSeasonRecordAtsRefToTeamSeasonRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.TeamSeasonRecordAtsRefToTeamSeasonRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/395/projection")] // no seasons segment
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/123/projection")] // wrong resource (athletes)
        public void TeamSeasonProjectionRefToTeamSeasonRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.TeamSeasonProjectionRefToTeamSeasonRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void TeamSeasonProjectionRefToTeamSeasonRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.TeamSeasonProjectionRefToTeamSeasonRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/395/awards/123")] // no seasons segment
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/123/awards/456")] // wrong resource (athletes)
        public void TeamSeasonAwardRefToTeamSeasonRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.TeamSeasonAwardRefToTeamSeasonRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void TeamSeasonAwardRefToTeamSeasonRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.TeamSeasonAwardRefToTeamSeasonRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region Competition Child Resource Mappers

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/broadcasts/123",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/broadcasts?lang=en",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172")]
        public void CompetitionBroadcastRefToCompetitionRef_Should_Map_To_Competition_Without_Query(
            string broadcastRef,
            string expectedCompetitionRef)
        {
            var input = new Uri(broadcastRef);
            var result = EspnUriMapper.CompetitionBroadcastRefToCompetitionRef(input);
            result.Should().Be(new Uri(expectedCompetitionRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/plays/4017526710011",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/plays?limit=25",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172")]
        public void CompetitionPlayRefToCompetitionRef_Should_Map_To_Competition_Without_Query(
            string playRef,
            string expectedCompetitionRef)
        {
            var input = new Uri(playRef);
            var result = EspnUriMapper.CompetitionPlayRefToCompetitionRef(input);
            result.Should().Be(new Uri(expectedCompetitionRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/prediction",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172/predictions?lang=en",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401540172/competitions/401540172")]
        public void CompetitionPredictionRefToCompetitionRef_Should_Map_To_Competition_Without_Query(
            string predictionRef,
            string expectedCompetitionRef)
        {
            var input = new Uri(predictionRef);
            var result = EspnUriMapper.CompetitionPredictionRefToCompetitionRef(input);
            result.Should().Be(new Uri(expectedCompetitionRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/status",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/status/1",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        public void CompetitionStatusRefToCompetitionRef_Should_Map_To_Competition_Without_Query(
            string statusRef,
            string expectedCompetitionRef)
        {
            var input = new Uri(statusRef);
            var result = EspnUriMapper.CompetitionStatusRefToCompetitionRef(input);
            result.Should().Be(new Uri(expectedCompetitionRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/situation",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/situation/1",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        public void CompetitionSituationRefToCompetitionRef_Should_Map_To_Competition_Without_Query(
            string situationRef,
            string expectedCompetitionRef)
        {
            var input = new Uri(situationRef);
            var result = EspnUriMapper.CompetitionSituationRefToCompetitionRef(input);
            result.Should().Be(new Uri(expectedCompetitionRef));
        }

        [Fact]
        public void CompetitionBroadcastRefToCompetitionRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CompetitionBroadcastRefToCompetitionRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CompetitionPlayRefToCompetitionRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CompetitionPlayRefToCompetitionRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CompetitionPredictionRefToCompetitionRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CompetitionPredictionRefToCompetitionRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CompetitionStatusRefToCompetitionRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CompetitionStatusRefToCompetitionRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CompetitionSituationRefToCompetitionRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CompetitionSituationRefToCompetitionRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/drives/4017526710012",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/drives",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        public void CompetitionDriveRefToCompetitionRef_Should_Map_To_Competition_Without_Query(
            string driveRef,
            string expectedCompetitionRef)
        {
            var input = new Uri(driveRef);
            var result = EspnUriMapper.CompetitionDriveRefToCompetitionRef(input);
            result.Should().Be(new Uri(expectedCompetitionRef));
        }

        [Fact]
        public void CompetitionDriveRefToCompetitionRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CompetitionDriveRefToCompetitionRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/odds",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/odds/1001",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        public void CompetitionOddsRefToCompetitionRef_Should_Map_To_Competition_Without_Query(
            string oddsRef,
            string expectedCompetitionRef)
        {
            var input = new Uri(oddsRef);
            var result = EspnUriMapper.CompetitionOddsRefToCompetitionRef(input);
            result.Should().Be(new Uri(expectedCompetitionRef));
        }

        [Fact]
        public void CompetitionOddsRefToCompetitionRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CompetitionOddsRefToCompetitionRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/powerindex",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/power-index",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/powerindex/1",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671/power-index/1",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752671/competitions/401752671")]
        public void CompetitionPowerIndexRefToCompetitionRef_Should_Map_To_Competition_Without_Query(
            string powerIndexRef,
            string expectedCompetitionRef)
        {
            var input = new Uri(powerIndexRef);
            var result = EspnUriMapper.CompetitionPowerIndexRefToCompetitionRef(input);
            result.Should().Be(new Uri(expectedCompetitionRef));
        }

        [Fact]
        public void CompetitionPowerIndexRefToCompetitionRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CompetitionPowerIndexRefToCompetitionRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region AthleteSeason Child Resource Mappers

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/4426333/statistics/0",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/4426333")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/-6952/statistics/1?lang=en",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/-6952")]
        public void AthleteSeasonStatisticsRefToAthleteSeasonRef_Should_Map_To_AthleteSeason_Without_Query(
            string statisticsRef,
            string expectedAthleteSeasonRef)
        {
            var input = new Uri(statisticsRef);
            var result = EspnUriMapper.AthleteSeasonStatisticsRefToAthleteSeasonRef(input);
            result.Should().Be(new Uri(expectedAthleteSeasonRef));
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/4426333/statistics/0")] // missing seasons segment
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/395/statistics/0")] // wrong resource (teams)
        public void AthleteSeasonStatisticsRefToAthleteSeasonRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.AthleteSeasonStatisticsRefToAthleteSeasonRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void AthleteSeasonStatisticsRefToAthleteSeasonRef_Should_Throw_On_NonNumeric_AthleteId()
        {
            // "athletes" segment exists but the id is not numeric — should throw, not silently pass through
            var input = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/not-an-id/statistics/0");
            Action act = () => EspnUriMapper.AthleteSeasonStatisticsRefToAthleteSeasonRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void AthleteSeasonStatisticsRefToAthleteSeasonRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.AthleteSeasonStatisticsRefToAthleteSeasonRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region Coach Resource Mappers

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/coaches/123/record",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/coaches/123")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/coaches/456/record?lang=en",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/coaches/456")]
        public void CoachSeasonRecordRefToCoachSeasonRef_Should_Map_To_CoachSeason_Without_Query(
            string recordRef,
            string expectedCoachSeasonRef)
        {
            var input = new Uri(recordRef);
            var result = EspnUriMapper.CoachSeasonRecordRefToCoachSeasonRef(input);
            result.Should().Be(new Uri(expectedCoachSeasonRef));
        }

        [Theory]
        [InlineData(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/123/record",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/123")]
        [InlineData(
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/456/record?lang=en",
            "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/456")]
        public void CoachRecordRefToCoachRef_Should_Map_To_Coach_Without_Query(
            string recordRef,
            string expectedCoachRef)
        {
            var input = new Uri(recordRef);
            var result = EspnUriMapper.CoachRecordRefToCoachRef(input);
            result.Should().Be(new Uri(expectedCoachRef));
        }

        [Fact]
        public void CoachSeasonRecordRefToCoachSeasonRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CoachSeasonRecordRefToCoachSeasonRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/players/123/record")] // wrong resource (players instead of coaches)
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/record")] // missing coaches segment entirely
        public void CoachSeasonRecordRefToCoachSeasonRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.CoachSeasonRecordRefToCoachSeasonRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void CoachRecordRefToCoachRef_Should_Throw_On_Null()
        {
            Action act = () => EspnUriMapper.CoachRecordRefToCoachRef(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/players/123/record")] // wrong resource (players instead of coaches)
        [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/record")] // missing coaches segment entirely
        public void CoachRecordRefToCoachRef_Should_Throw_On_Unexpected_Shape(string badRef)
        {
            var input = new Uri(badRef);
            Action act = () => EspnUriMapper.CoachRecordRefToCoachRef(input);
            act.Should().Throw<InvalidOperationException>();
        }

        #endregion
    }
}
