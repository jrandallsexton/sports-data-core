using FluentAssertions;

using MassTransit;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Events;
using SportsData.Api.Application.UI.PlayerLineups.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Eventing.Events.Athletes;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Infrastructure.Clients.Athlete;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Events;

/// <summary>
/// Phase 2 scoring consumers: the stats-updated trigger persists slot
/// points + lineup totals; contest finalization freezes slots so later
/// stat events cannot move a final number. Shares one seeded world.
/// </summary>
public class AthleteCompetitionStatsUpdatedHandlerTests : ApiTestBase<AthleteCompetitionStatsUpdatedHandler>
{
    private static readonly Guid LeagueId = Guid.NewGuid();
    private static readonly DateTime FixedNow = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ContestId = Guid.NewGuid();
    private static readonly Guid AthleteSeasonId = Guid.NewGuid();

    public AthleteCompetitionStatsUpdatedHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 8, 27, 20, 0, 0, DateTimeKind.Utc));

        // Real scorer — the consumers' behavior IS the scorer's writes.
        Mocker.Use<IPlayerLineupScorer>(Mocker.CreateInstance<PlayerLineupScorer>());

        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.All).Returns(new Mock<IClientProxy>().Object);
        Mocker.GetMock<IHubContext<NotificationHub>>()
            .Setup(x => x.Clients).Returns(hubClients.Object);
    }

    private void SetStatline(Dictionary<string, decimal> stats)
    {
        var client = new Mock<IProvideAthletes>();
        client
            .Setup(x => x.GetAthleteStatlines(It.IsAny<List<Guid>>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<AthleteStatlineDto>>(
            [
                new AthleteStatlineDto
                {
                    AthleteSeasonId = AthleteSeasonId,
                    ContestId = ContestId,
                    Stats = stats,
                },
            ]));
        Mocker.GetMock<IAthleteClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(client.Object);
    }

    private async Task SeedWorldAsync(bool slotAlreadyFinal = false)
    {
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = LeagueId,
            Name = "PP",
            Sport = Sport.FootballNfl,
            League = League.NFL,
            CommissionerUserId = UserId,
            SeasonYear = 2026,
            GroupType = GroupType.PlayerPickem,
        });
        DataContext.PlayerScoringRuleSets.Add(new PlayerScoringRuleSet
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            IsDefault = true,
            CreatedUtc = FixedNow,
        });
        var ruleSetId = DataContext.PlayerScoringRuleSets.Local.First().Id;
        DataContext.PlayerScoringRules.AddRange(
            new PlayerScoringRule { Id = Guid.NewGuid(), RuleSetId = ruleSetId, StatKey = "passing.passingYards", Points = 1m, PerUnits = 25m },
            new PlayerScoringRule { Id = Guid.NewGuid(), RuleSetId = ruleSetId, StatKey = "passing.passingTouchdowns", Points = 6m, PerUnits = 1m });

        var lineup = new PlayerLineup
        {
            Id = Guid.NewGuid(),
            PickemGroupId = LeagueId,
            UserId = UserId,
            SeasonYear = 2026,
            SeasonWeek = 4,
            CreatedUtc = FixedNow,
            CreatedBy = UserId,
        };
        lineup.Slots.Add(new PlayerLineupSlot
        {
            Id = Guid.NewGuid(),
            PlayerLineupId = lineup.Id,
            SlotId = "QB",
            AthleteId = Guid.NewGuid(),
            AthleteSeasonId = AthleteSeasonId,
            Position = "QB",
            FirstName = "Arch",
            LastName = "Manning",
            TeamName = "Team",
            TeamSlug = "team",
            ContestId = ContestId,
            IsScoreFinal = slotAlreadyFinal,
            CreatedUtc = FixedNow,
            CreatedBy = UserId,
        });
        DataContext.PlayerLineups.Add(lineup);
        await DataContext.SaveChangesAsync();
    }

    private static ConsumeContext<T> Ctx<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.Setup(x => x.Message).Returns(message);
        ctx.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    [Fact]
    public async Task StatsUpdated_PersistsSlotPoints_AndLineupTotal()
    {
        await SeedWorldAsync();
        SetStatline(new Dictionary<string, decimal>
        {
            ["passing.passingYards"] = 187m,
            ["passing.passingTouchdowns"] = 2m,
        });
        var handler = Mocker.CreateInstance<AthleteCompetitionStatsUpdatedHandler>();

        await handler.Consume(Ctx(new AthleteCompetitionStatsUpdated(
            ContestId, Guid.NewGuid(), AthleteSeasonId, null, Sport.FootballNfl, 2026, Guid.NewGuid(), Guid.NewGuid())));

        var slot = await DataContext.PlayerLineupSlots.SingleAsync();
        slot.Points.Should().Be(19.48m); // 187/25 = 7.48 + 2 TD * 6
        slot.StatLine.Should().Contain("187 PaYd");
        slot.IsScoreFinal.Should().BeFalse();
        var lineup = await DataContext.PlayerLineups.SingleAsync();
        lineup.TotalPoints.Should().Be(19.48m);
        lineup.ScoreUpdatedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task StatsUpdated_SkipsFrozenSlots()
    {
        await SeedWorldAsync(slotAlreadyFinal: true);
        SetStatline(new Dictionary<string, decimal> { ["passing.passingYards"] = 999m });
        var handler = Mocker.CreateInstance<AthleteCompetitionStatsUpdatedHandler>();

        await handler.Consume(Ctx(new AthleteCompetitionStatsUpdated(
            ContestId, Guid.NewGuid(), AthleteSeasonId, null, Sport.FootballNfl, 2026, Guid.NewGuid(), Guid.NewGuid())));

        var slot = await DataContext.PlayerLineupSlots.SingleAsync();
        slot.Points.Should().BeNull(); // frozen slot untouched
    }

    [Fact]
    public async Task ContestFinalized_RecomputesAndFreezes()
    {
        await SeedWorldAsync();
        SetStatline(new Dictionary<string, decimal>
        {
            ["passing.passingYards"] = 250m, // 10.00
        });
        var handler = Mocker.CreateInstance<PlayerLineupContestFinalizedHandler>();

        await handler.Consume(Ctx(new ContestFinalized(
            ContestId, null, Sport.FootballNfl, 2026, Guid.NewGuid(), Guid.NewGuid())));

        var slot = await DataContext.PlayerLineupSlots.SingleAsync();
        slot.Points.Should().Be(10.00m);
        slot.IsScoreFinal.Should().BeTrue();
        (await DataContext.PlayerLineups.SingleAsync()).TotalPoints.Should().Be(10.00m);
    }
}
