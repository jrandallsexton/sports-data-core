using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.PlayerLineups.Commands.ClearLineupSlot;
using SportsData.Api.Application.UI.PlayerLineups.Commands.UpsertLineupSlot;
using SportsData.Api.Application.UI.PlayerLineups.Queries.GetMyPlayerLineup;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.PlayerLineups;

/// <summary>
/// Roster persistence: derived per-player locking (kickoff−5, the
/// product-wide rule), server-authoritative contest anchoring, the lazy
/// carry-over clone, and the enablement gate. Lock scenarios pivot on a
/// fixed clock; the week's matchups come from a mocked ContestClient.
/// </summary>
public class PlayerLineupHandlerTests : ApiTestBase<UpsertLineupSlotCommandHandler>
{
    private static readonly DateTime Now = new(2026, 9, 5, 16, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LeagueId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IProvideContests> _contestClient = new();

    public PlayerLineupHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClient.Object);
        SetWeekMatchups([]);
    }

    private void SetWeekMatchups(List<Matchup> matchups) =>
        _contestClient
            .Setup(x => x.GetMatchupsForSeasonWeek(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<Matchup>>(matchups));

    private static Matchup MakeMatchup(string homeSlug, string awaySlug, DateTime startUtc) => new()
    {
        ContestId = Guid.NewGuid(),
        SeasonYear = 2026,
        SeasonWeek = 2,
        SeasonWeekId = Guid.NewGuid(),
        StartDateUtc = startUtc,
        Status = "STATUS_SCHEDULED",
        StatusDescription = "Scheduled",
        HomeSlug = homeSlug,
        AwaySlug = awaySlug,
    };

    private async Task SeedLeagueAsync(GroupType groupType = GroupType.PlayerPickem)
    {
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = LeagueId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            CommissionerUserId = Guid.NewGuid(),
            SeasonYear = 2026,
            GroupType = groupType,
        });
        await DataContext.SaveChangesAsync();
    }

    private static UpsertLineupSlotCommand QbCommand(string slotId = "QB") => new()
    {
        LeagueId = LeagueId,
        UserId = UserId,
        SeasonYear = 2026,
        SeasonWeek = 2,
        SlotId = slotId,
        AthleteId = Guid.NewGuid(),
        AthleteSeasonId = Guid.NewGuid(),
        Position = "QB",
        FirstName = "Arch",
        LastName = "Manning",
        TeamName = "Texas Longhorns",
        TeamSlug = "texas-longhorns",
        OpponentName = "Oklahoma Sooners",
    };

    // ── Gate ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task TeamPickemLeague_IsForbidden()
    {
        // One game per league: a TeamPickem group does not play this game.
        await SeedLeagueAsync(GroupType.TeamPickem);
        var handler = Mocker.CreateInstance<UpsertLineupSlotCommandHandler>();

        var result = await handler.ExecuteAsync(QbCommand());

        result.Status.Should().Be(ResultStatus.Forbid);
    }

    [Fact]
    public async Task NonMember_IsForbidden()
    {
        await SeedLeagueAsync();
        DenyLeagueMembership();
        var handler = Mocker.CreateInstance<UpsertLineupSlotCommandHandler>();

        var result = await handler.ExecuteAsync(QbCommand());

        result.Status.Should().Be(ResultStatus.Forbid);
    }

    // ── Upsert rules ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("BENCH", "QB")]  // unknown slot
    [InlineData("QB", "RB")]     // ineligible position
    [InlineData("FLEX", "QB")]   // QB is not FLEX-eligible
    public async Task ShapeViolations_FailValidation(string slotId, string position)
    {
        await SeedLeagueAsync();
        var handler = Mocker.CreateInstance<UpsertLineupSlotCommandHandler>();

        var command = QbCommand(slotId);
        command.Position = position;
        var result = await handler.ExecuteAsync(command);

        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task HappyPath_CreatesLineup_AndStoresServerResolvedAnchor()
    {
        await SeedLeagueAsync();
        var kickoff = Now.AddHours(3);
        var matchup = MakeMatchup("texas-longhorns", "oklahoma-sooners", kickoff);
        SetWeekMatchups([matchup]);
        var handler = Mocker.CreateInstance<UpsertLineupSlotCommandHandler>();

        var result = await handler.ExecuteAsync(QbCommand());

        result.IsSuccess.Should().BeTrue();
        var slot = await DataContext.PlayerLineupSlots.SingleAsync();
        // The anchor is what the SERVER resolved — the command carries none.
        slot.ContestId.Should().Be(matchup.ContestId);
        slot.ContestStartUtc.Should().Be(kickoff);
        result.Value.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task ByeAthlete_SavesWithNullAnchor()
    {
        await SeedLeagueAsync();
        SetWeekMatchups([]); // team not on the slate
        var handler = Mocker.CreateInstance<UpsertLineupSlotCommandHandler>();

        var result = await handler.ExecuteAsync(QbCommand());

        result.IsSuccess.Should().BeTrue();
        var slot = await DataContext.PlayerLineupSlots.SingleAsync();
        slot.ContestId.Should().BeNull();
        slot.ContestStartUtc.Should().BeNull();
    }

    [Fact]
    public async Task DuplicateAthleteAcrossSlots_FailsValidation()
    {
        await SeedLeagueAsync();
        var handler = Mocker.CreateInstance<UpsertLineupSlotCommandHandler>();
        var first = QbCommand("RB1");
        first.Position = "RB";
        (await handler.ExecuteAsync(first)).IsSuccess.Should().BeTrue();

        var second = QbCommand("RB2");
        second.Position = "RB";
        second.AthleteId = first.AthleteId; // same athlete, different slot
        var result = await handler.ExecuteAsync(second);

        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task IncomingAthleteWithLockedGame_FailsValidation()
    {
        await SeedLeagueAsync();
        // Kickoff 3 minutes out — inside the 5-minute lock window.
        SetWeekMatchups([MakeMatchup("texas-longhorns", "oklahoma-sooners", Now.AddMinutes(3))]);
        var handler = Mocker.CreateInstance<UpsertLineupSlotCommandHandler>();

        var result = await handler.ExecuteAsync(QbCommand());

        result.Status.Should().Be(ResultStatus.Validation);
        (await DataContext.PlayerLineupSlots.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task TargetSlotWithLockedOccupant_RejectsReplacement()
    {
        await SeedLeagueAsync();
        // Occupant's game is comfortably future at first save…
        SetWeekMatchups([MakeMatchup("texas-longhorns", "oklahoma-sooners", Now.AddMinutes(4))]);
        var handler = Mocker.CreateInstance<UpsertLineupSlotCommandHandler>();
        var occupant = QbCommand();
        // …seed the occupant directly with an already-locked anchor.
        await SeedLeagueLineupWithSlotAsync(occupant, contestStartUtc: Now.AddMinutes(-30));

        var replacement = QbCommand();
        var result = await handler.ExecuteAsync(replacement);

        result.Status.Should().Be(ResultStatus.Validation);
        (await DataContext.PlayerLineupSlots.SingleAsync()).AthleteId.Should().Be(occupant.AthleteId);
    }

    [Fact]
    public async Task ClonedNullAnchorOccupant_WhoseGameIsLive_RejectsReplacement()
    {
        // The carry-over hole: a cloned slot can hold a NULL anchor. The
        // occupant's team currently resolves to a locked game, so the swap
        // must be rejected even though the stored anchor says nothing.
        await SeedLeagueAsync();
        var occupant = QbCommand();
        await SeedLeagueLineupWithSlotAsync(occupant, contestStartUtc: null);
        SetWeekMatchups([MakeMatchup(occupant.TeamSlug, "oklahoma-sooners", Now.AddMinutes(-10))]);
        var handler = Mocker.CreateInstance<UpsertLineupSlotCommandHandler>();

        var replacement = QbCommand();
        replacement.TeamSlug = "lsu-tigers"; // incoming athlete not on the locked team
        var result = await handler.ExecuteAsync(replacement);

        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task MatchupResolutionFailure_FailsClosed_AndPersistsNothing()
    {
        await SeedLeagueAsync();
        _contestClient
            .Setup(x => x.GetMatchupsForSeasonWeek(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<List<Matchup>>(default!, ResultStatus.Error, []));
        var handler = Mocker.CreateInstance<UpsertLineupSlotCommandHandler>();

        var result = await handler.ExecuteAsync(QbCommand());

        result.Status.Should().Be(ResultStatus.Error);
        (await DataContext.PlayerLineups.AnyAsync()).Should().BeFalse();
    }

    // ── Clear ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Clear_LockedSlot_FailsValidation_UnlockedSlot_Removes()
    {
        await SeedLeagueAsync();
        var locked = QbCommand("QB");
        await SeedLeagueLineupWithSlotAsync(locked, contestStartUtc: Now.AddMinutes(-30));
        var handler = Mocker.CreateInstance<ClearLineupSlotCommandHandler>();

        var lockedResult = await handler.ExecuteAsync(
            new ClearLineupSlotCommand(LeagueId, UserId, 2026, 2, "QB"));
        lockedResult.Status.Should().Be(ResultStatus.Validation);

        // Unlock by moving the anchor to the future, then clear succeeds.
        var slot = await DataContext.PlayerLineupSlots.SingleAsync();
        slot.ContestStartUtc = Now.AddHours(5);
        await DataContext.SaveChangesAsync();

        var clearedResult = await handler.ExecuteAsync(
            new ClearLineupSlotCommand(LeagueId, UserId, 2026, 2, "QB"));
        clearedResult.IsSuccess.Should().BeTrue();
        (await DataContext.PlayerLineupSlots.AnyAsync()).Should().BeFalse();
    }

    // ── Read + lazy clone ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_NoLineupNoPrior_ReturnsEmptySlots_AndCreatesNothing()
    {
        await SeedLeagueAsync();
        var handler = Mocker.CreateInstance<GetMyPlayerLineupQueryHandler>();

        var result = await handler.ExecuteAsync(new GetMyPlayerLineupQuery(LeagueId, UserId, 2026, 2));

        result.IsSuccess.Should().BeTrue();
        result.Value.Slots.Should().BeEmpty();
        (await DataContext.PlayerLineups.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Get_ClonesPriorWeek_ReresolvingContests()
    {
        await SeedLeagueAsync();
        var occupant = QbCommand();
        await SeedLeagueLineupWithSlotAsync(occupant, contestStartUtc: Now.AddDays(-6), seasonWeek: 1);

        // Week 2: the occupant's team plays a NEW game.
        var week2 = MakeMatchup(occupant.TeamSlug, "georgia-bulldogs", Now.AddDays(1));
        SetWeekMatchups([week2]);
        var handler = Mocker.CreateInstance<GetMyPlayerLineupQueryHandler>();

        var result = await handler.ExecuteAsync(new GetMyPlayerLineupQuery(LeagueId, UserId, 2026, 2));

        result.IsSuccess.Should().BeTrue();
        var slot = result.Value.Slots.Should().ContainSingle().Subject;
        slot.AthleteId.Should().Be(occupant.AthleteId);
        slot.ContestId.Should().Be(week2.ContestId);          // re-resolved, not carried
        slot.ContestStartUtc.Should().Be(week2.StartDateUtc);
        slot.IsLocked.Should().BeFalse();

        (await DataContext.PlayerLineups.CountAsync()).Should().Be(2); // week 1 + cloned week 2
    }

    [Fact]
    public async Task Get_CloneSkipsWhenMatchupResolutionFails_ServesEmpty()
    {
        await SeedLeagueAsync();
        await SeedLeagueLineupWithSlotAsync(QbCommand(), contestStartUtc: Now.AddDays(-6), seasonWeek: 1);
        _contestClient
            .Setup(x => x.GetMatchupsForSeasonWeek(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<List<Matchup>>(default!, ResultStatus.Error, []));
        var handler = Mocker.CreateInstance<GetMyPlayerLineupQueryHandler>();

        var result = await handler.ExecuteAsync(new GetMyPlayerLineupQuery(LeagueId, UserId, 2026, 2));

        // Fail open on the read: empty week now, retry next read.
        result.IsSuccess.Should().BeTrue();
        result.Value.Slots.Should().BeEmpty();
        (await DataContext.PlayerLineups.CountAsync()).Should().Be(1); // week 1 only
    }

    [Fact]
    public async Task Get_DerivesIsLocked_FromTheSharedRule()
    {
        await SeedLeagueAsync();
        // Kickoff 3 minutes out — locked under kickoff−5.
        await SeedLeagueLineupWithSlotAsync(QbCommand(), contestStartUtc: Now.AddMinutes(3), seasonWeek: 2);
        var handler = Mocker.CreateInstance<GetMyPlayerLineupQueryHandler>();

        var result = await handler.ExecuteAsync(new GetMyPlayerLineupQuery(LeagueId, UserId, 2026, 2));

        result.Value.Slots.Single().IsLocked.Should().BeTrue();
    }

    // ── Seed helper ───────────────────────────────────────────────────────

    private async Task SeedLeagueLineupWithSlotAsync(
        UpsertLineupSlotCommand source,
        DateTime? contestStartUtc,
        int seasonWeek = 2)
    {
        var lineup = new PlayerLineup
        {
            Id = Guid.NewGuid(),
            PickemGroupId = LeagueId,
            UserId = UserId,
            SeasonYear = 2026,
            SeasonWeek = seasonWeek,
        };
        lineup.Slots.Add(new PlayerLineupSlot
        {
            Id = Guid.NewGuid(),
            PlayerLineupId = lineup.Id,
            SlotId = source.SlotId,
            AthleteId = source.AthleteId,
            AthleteSeasonId = source.AthleteSeasonId,
            Position = source.Position,
            FirstName = source.FirstName,
            LastName = source.LastName,
            TeamName = source.TeamName,
            TeamSlug = source.TeamSlug,
            ContestId = contestStartUtc.HasValue ? Guid.NewGuid() : null,
            ContestStartUtc = contestStartUtc,
        });
        DataContext.PlayerLineups.Add(lineup);
        await DataContext.SaveChangesAsync();
    }
}
