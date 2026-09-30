using FluentAssertions;

using SportsData.Producer.Infrastructure.Sql;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Infrastructure.Sql;

public class ProducerSqlQueryProviderTests
{
    /// <summary>
    /// Wiring check for the GetTeamFinalizedGames resource. The SQL itself
    /// can't be executed without a real Postgres, but loading + a couple of
    /// shape assertions catch the common build-time failure modes:
    ///   - resource not embedded (csproj or _fileNames typo),
    ///   - as-of-date filter accidentally dropped or inverted,
    ///   - finalized-only filter accidentally dropped.
    /// </summary>
    [Fact]
    public void GetTeamFinalizedGames_LoadsAndContainsExpectedFilters()
    {
        var sut = new ProducerSqlQueryProvider();

        var sql = sut.GetTeamFinalizedGames();

        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().Contain("\"FinalizedUtc\" IS NOT NULL");
        // Inclusive as-of-date cutoff — fixes MLB same-week games and football
        // postseason reuse-of-Week-1 issues that a numeric week filter mishandled.
        // See docs/team-finalized-games-endpoint.md.
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

    /// <summary>
    /// The odds-pricing query must pick the SAME provider row as the matchup
    /// queries (so prices pair with the matchup's spread/total), join BOTH
    /// team sides, and project every OddsPricingDto property by name (Dapper
    /// maps by alias, so a dropped or renamed alias silently yields nulls).
    /// </summary>
    [Fact]
    public void GetOddsPricingByContestId_UsesTheMatchupProviderRow_AndProjectsEveryDtoField()
    {
        var sut = new ProducerSqlQueryProvider();

        var sql = sut.GetOddsPricingByContestId();

        sql.Should().NotBeNullOrWhiteSpace();
        // Placeholders resolved to the same preferred/fallback pair as the matchups SQL.
        sql.Should().Contain("\"ProviderId\" IN ('58', '100')");
        sql.Should().Contain("WHEN o.\"ProviderId\" = '58' THEN 1");
        sut.GetMatchupsByContestIds().Should().Contain("\"ProviderId\" IN ('58', '100')");

        sql.Should().Contain("away.\"Side\" = 'Away'");
        sql.Should().Contain("home.\"Side\" = 'Home'");
        sql.Should().Contain("WHERE c.\"Id\" = @ContestId");

        foreach (var property in typeof(SportsData.Core.Dtos.Canonical.OddsPricingDto).GetProperties())
        {
            sql.Should().Contain($"AS \"{property.Name}\"", $"Dapper maps {property.Name} by column alias");
        }
    }
}
