using FluentAssertions;

using SportsData.Producer.Infrastructure.Sql;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Infrastructure.Sql;

public class ProducerSqlQueryProviderTests
{
    /// <summary>
    /// Wiring check for the GetTeamScheduleCompleted resource. The SQL itself
    /// can't be executed without a real Postgres, but loading + a couple of
    /// shape assertions catch the common build-time failure modes:
    ///   - resource not embedded (csproj or _fileNames typo),
    ///   - as-of-date filter accidentally dropped or inverted,
    ///   - completed-only filter accidentally dropped.
    /// </summary>
    [Fact]
    public void GetTeamScheduleCompleted_LoadsAndContainsExpectedFilters()
    {
        var sut = new ProducerSqlQueryProvider();

        var sql = sut.GetTeamScheduleCompleted();

        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().Contain("\"FinalizedUtc\" IS NOT NULL");
        // Inclusive as-of-date cutoff — fixes MLB same-week games and football
        // postseason reuse-of-Week-1 issues that a numeric week filter mishandled.
        // See docs/team-schedule-endpoint.md.
        sql.Should().Contain("@AsOfDate IS NULL OR C.\"FinalizedUtc\" <= @AsOfDate");
        // Newest-first; endpoint contract relies on this so the client doesn't reverse.
        sql.Should().Contain("ORDER BY C.\"StartDateUtc\" DESC");
    }

    /// <summary>
    /// The matchups query feeds LeagueWeekMatchupsDto.AsOfDate via SeasonWeek.EndDate.
    /// Guards against accidental removal of the SeasonWeek JOIN/projection.
    /// </summary>
    [Fact]
    public void GetMatchupsByContestIds_ExposesSeasonWeekEndDate()
    {
        var sut = new ProducerSqlQueryProvider();

        var sql = sut.GetMatchupsByContestIds();

        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().Contain("sw_contest.\"EndDate\" AS \"SeasonWeekEndDate\"");
        sql.Should().Contain("public.\"SeasonWeek\" sw_contest");
    }
}
