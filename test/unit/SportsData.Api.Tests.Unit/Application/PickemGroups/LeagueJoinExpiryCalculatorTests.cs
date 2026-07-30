using FluentAssertions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.PickemGroups;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Season;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.PickemGroups;

/// <summary>
/// The stored-expiry computation — one branch per rule in the v2 design
/// (docs/features/league-join-policy-and-discovery.md, "v2 revision").
/// </summary>
public class LeagueJoinExpiryCalculatorTests : ApiTestBase<LeagueJoinExpiryCalculator>
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    private LeagueJoinExpiryCalculator CreateCalculator(SeasonOverviewDto? overview = null)
    {
        var client = new Mock<IProvideSeasons>();
        if (overview is not null)
        {
            client.Setup(x => x.GetSeasonOverview(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<SeasonOverviewDto>(overview));
        }
        else
        {
            client.Setup(x => x.GetSeasonOverview(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("producer unreachable"));
        }

        Mocker.GetMock<ISeasonClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(client.Object);

        return Mocker.CreateInstance<LeagueJoinExpiryCalculator>();
    }

    private async Task<PickemGroup> SeedLeagueAsync(
        JoinPolicy policy,
        int? dropLowWeeks = null,
        DateTime? startsOn = null,
        DateTime? endsOn = null,
        DateTime? deactivatedUtc = null,
        DateTime? existingExpiry = null)
    {
        // Window shape is explicit on the entity; the seeds mirror what the
        // creation handler would store (dates -> DateRange, else FullSeason).
        var window = startsOn is null && endsOn is null
            ? LeagueWindow.FullSeason
            : LeagueWindow.DateRange;
        var league = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test League",
            CommissionerUserId = Guid.NewGuid(),
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            SeasonYear = 2026,
            JoinPolicy = policy,
            LeagueWindow = window,
            DropLowWeeksCount = dropLowWeeks,
            StartsOn = startsOn,
            EndsOn = endsOn,
            DeactivatedUtc = deactivatedUtc,
            InvitationsExpireUtc = existingExpiry,
            CreatedUtc = Now,
            CreatedBy = Guid.NewGuid()
        };
        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.SaveChangesAsync();
        return league;
    }

    private async Task SeedMatchupAsync(Guid groupId, int seasonWeek, DateTime startUtc)
    {
        await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = Guid.NewGuid(),
            SeasonYear = 2026,
            SeasonWeek = seasonWeek,
            StartDateUtc = startUtc,
            CreatedUtc = Now,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    private static SeasonOverviewDto Overview() => new()
    {
        SeasonYear = 2026,
        Name = "2026",
        StartDate = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2027, 1, 20, 0, 0, 0, DateTimeKind.Utc),
        Weeks =
        [
            new SeasonWeekDto { Id = Guid.NewGuid(), Number = 3, SeasonPhaseName = "Regular Season", StartDate = new DateTime(2026, 9, 8), EndDate = new DateTime(2026, 9, 14) },
            new SeasonWeekDto { Id = Guid.NewGuid(), Number = 4, SeasonPhaseName = "Regular Season", StartDate = new DateTime(2026, 9, 15), EndDate = new DateTime(2026, 9, 21) },
            // Postseason numbering restarts — must NOT satisfy a week-4 lookup.
            new SeasonWeekDto { Id = Guid.NewGuid(), Number = 4, SeasonPhaseName = "Postseason", StartDate = new DateTime(2027, 1, 1), EndDate = new DateTime(2027, 1, 8) }
        ]
    };

    // ── Drop-week rule (FullSeason default) ───────────────────────────────────

    [Fact]
    public async Task DropWeeks_FullSeason_UsesFirstKickoffOfWeekAfterDroppedWindow()
    {
        // 3 drop weeks -> expiry at week 4's first kickoff, regardless of the
        // commissioner's CloseAtFirstGame choice.
        var league = await SeedLeagueAsync(JoinPolicy.CloseAtFirstGame, dropLowWeeks: 3);
        await SeedMatchupAsync(league.Id, seasonWeek: 1, new DateTime(2026, 8, 30, 16, 0, 0, DateTimeKind.Utc));
        var week4Kickoff = new DateTime(2026, 9, 17, 23, 0, 0, DateTimeKind.Utc);
        await SeedMatchupAsync(league.Id, seasonWeek: 4, week4Kickoff);
        await SeedMatchupAsync(league.Id, seasonWeek: 4, week4Kickoff.AddHours(3));

        await CreateCalculator(Overview()).RecomputeAsync(league.Id);

        league.InvitationsExpireUtc.Should().Be(week4Kickoff);
    }

    [Fact]
    public async Task DropWeeks_FullSeason_FallsBackToCalendarWeekBoundary()
    {
        // Full-season slates build progressively — week 4's matchups may not
        // exist yet. Provisional value = the calendar's regular-season week-4
        // start (NOT the postseason week 4 — numbering restarts).
        var league = await SeedLeagueAsync(JoinPolicy.Open, dropLowWeeks: 3);
        await SeedMatchupAsync(league.Id, seasonWeek: 1, new DateTime(2026, 8, 30, 16, 0, 0, DateTimeKind.Utc));

        await CreateCalculator(Overview()).RecomputeAsync(league.Id);

        league.InvitationsExpireUtc.Should().Be(new DateTime(2026, 9, 15));
    }

    // ── CloseAtFirstGame ──────────────────────────────────────────────────────

    [Fact]
    public async Task CloseAtFirstGame_UsesFirstGameStart()
    {
        var league = await SeedLeagueAsync(JoinPolicy.CloseAtFirstGame,
            startsOn: new DateTime(2026, 9, 1), endsOn: new DateTime(2026, 9, 30));
        var first = new DateTime(2026, 9, 5, 17, 0, 0, DateTimeKind.Utc);
        await SeedMatchupAsync(league.Id, 1, first.AddHours(4));
        await SeedMatchupAsync(league.Id, 1, first);

        await CreateCalculator(Overview()).RecomputeAsync(league.Id);

        league.InvitationsExpireUtc.Should().Be(first);
    }

    [Fact]
    public async Task CloseAtFirstGame_EmptySlate_LeavesExpiryUnset()
    {
        var league = await SeedLeagueAsync(JoinPolicy.CloseAtFirstGame,
            startsOn: new DateTime(2026, 9, 1), endsOn: new DateTime(2026, 9, 30));

        await CreateCalculator(Overview()).RecomputeAsync(league.Id);

        league.InvitationsExpireUtc.Should().BeNull("nothing has started; a later trigger fills this in");
    }

    // ── Open ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Open_WindowedLeague_UsesAuthoredEndsOn()
    {
        var endsOn = new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc);
        var league = await SeedLeagueAsync(JoinPolicy.Open,
            startsOn: new DateTime(2026, 9, 1), endsOn: endsOn);

        await CreateCalculator(Overview()).RecomputeAsync(league.Id);

        league.InvitationsExpireUtc.Should().Be(endsOn);
    }

    [Fact]
    public async Task Open_FullSeason_UsesSeasonCalendarEnd()
    {
        // "Open" no longer means forever — the old anchor (DeactivatedUtc)
        // never fires for full-season leagues (EndsOn is null).
        var league = await SeedLeagueAsync(JoinPolicy.Open);

        await CreateCalculator(Overview()).RecomputeAsync(league.Id);

        league.InvitationsExpireUtc.Should().Be(Overview().EndDate);
    }

    // ── Robustness ────────────────────────────────────────────────────────────

    [Fact]
    public async Task NeverOverwritesKnownValueWithUnknown()
    {
        // Producer unreachable -> computation yields null -> the previously
        // stored value must survive.
        var existing = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
        var league = await SeedLeagueAsync(JoinPolicy.Open, existingExpiry: existing);

        await CreateCalculator(overview: null).RecomputeAsync(league.Id);

        league.InvitationsExpireUtc.Should().Be(existing);
    }

    [Fact]
    public async Task DeactivatedLeague_IsSkipped()
    {
        var league = await SeedLeagueAsync(JoinPolicy.Open,
            endsOn: new DateTime(2026, 6, 30), deactivatedUtc: Now.AddDays(-10));

        await CreateCalculator(Overview()).RecomputeAsync(league.Id);

        league.InvitationsExpireUtc.Should().BeNull("deactivated leagues are off every joinable surface already");
    }
}
