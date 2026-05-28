using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.PickemGroups;
using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.PickemGroups;

/// <summary>
/// Pins the PR-B bootstrap-mode dispatch on <see cref="PickemGroupCreatedHandler"/>.
/// Three behavioral contracts:
///   1. Missing group is permanent, not transient — log and return, don't throw.
///   2. Future-start league (<see cref="PickemGroup.StartsOn"/> &gt; now) defers
///      to MatchupScheduler — no PickemGroupWeek row, no matchup enqueue.
///   3. Immediate path (StartsOn null or already past) hits the current-week
///      bootstrap and enqueues matchup generation, OR throws transient when
///      the season client can't resolve current week.
/// </summary>
public class PickemGroupCreatedHandlerTests : ApiTestBase<PickemGroupCreatedHandler>
{
    private static readonly DateTime FixedNow = new(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ISeasonClientFactory> _seasonClientFactoryMock;
    private readonly Mock<IProvideSeasons> _seasonClientMock;
    private readonly Mock<IProvideBackgroundJobs> _backgroundJobsMock;

    public PickemGroupCreatedHandlerTests()
    {
        _seasonClientFactoryMock = Mocker.GetMock<ISeasonClientFactory>();
        _seasonClientMock = new Mock<IProvideSeasons>();
        _seasonClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_seasonClientMock.Object);

        _backgroundJobsMock = Mocker.GetMock<IProvideBackgroundJobs>();

        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }

    [Fact]
    public async Task GroupNotFound_LogsAndReturns_DoesNotThrow()
    {
        // Permanent failure: don't DLQ. The group will never materialize on
        // retry. Log + return is the correct response.
        var sut = Mocker.CreateInstance<PickemGroupCreatedHandler>();
        var context = ConsumeContextFor(new PickemGroupCreated(
            Guid.NewGuid(), null, Sport.BaseballMlb, 2026, Guid.NewGuid(), Guid.NewGuid()));

        var act = async () => await sut.Consume(context);

        await act.Should().NotThrowAsync();
        _backgroundJobsMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FutureStartLeague_NoBootstrap_NoEnqueue()
    {
        // Motivating bug regression test: a league starting in the future
        // must not produce an orphan PickemGroupWeek for *today's* current
        // week, and must not enqueue matchup generation. The daily
        // MatchupScheduler picks the league up once now >= StartsOn (gated
        // in PR-D).
        var groupId = await SeedGroup(startsOn: FixedNow.AddDays(10));

        var sut = Mocker.CreateInstance<PickemGroupCreatedHandler>();
        var context = ConsumeContextFor(new PickemGroupCreated(
            groupId, null, Sport.BaseballMlb, 2026, Guid.NewGuid(), Guid.NewGuid()));

        await sut.Consume(context);

        (await DataContext.PickemGroupWeeks.AnyAsync(x => x.GroupId == groupId))
            .Should().BeFalse();
        _backgroundJobsMock.Verify(
            x => x.Enqueue<IScheduleGroupWeekMatchups>(It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task NullStartsOnLeague_CreatesWeek_AndEnqueues()
    {
        // Full-season league — Immediate path.
        var groupId = await SeedGroup(startsOn: null);
        SetupCurrentWeek(NewCurrentWeek());

        var sut = Mocker.CreateInstance<PickemGroupCreatedHandler>();
        var context = ConsumeContextFor(new PickemGroupCreated(
            groupId, null, Sport.BaseballMlb, 2026, Guid.NewGuid(), Guid.NewGuid()));

        await sut.Consume(context);

        (await DataContext.PickemGroupWeeks.AnyAsync(x => x.GroupId == groupId))
            .Should().BeTrue();
        _backgroundJobsMock.Verify(
            x => x.Enqueue<IScheduleGroupWeekMatchups>(It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartsOnInPastLeague_CreatesWeek_AndEnqueues()
    {
        // Validator already rejected the "ends-in-past" case; this exercises
        // the half-played single-day scenario where StartsOn was earlier
        // today (or the validator-allowed boundary race where StartsOn was
        // just-barely-future at validation time but the event-handler clock
        // has rolled past it).
        var groupId = await SeedGroup(startsOn: FixedNow.AddHours(-2));
        SetupCurrentWeek(NewCurrentWeek());

        var sut = Mocker.CreateInstance<PickemGroupCreatedHandler>();
        var context = ConsumeContextFor(new PickemGroupCreated(
            groupId, null, Sport.BaseballMlb, 2026, Guid.NewGuid(), Guid.NewGuid()));

        await sut.Consume(context);

        (await DataContext.PickemGroupWeeks.AnyAsync(x => x.GroupId == groupId))
            .Should().BeTrue();
        _backgroundJobsMock.Verify(
            x => x.Enqueue<IScheduleGroupWeekMatchups>(It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()),
            Times.Once);
    }

    [Fact]
    public async Task CurrentWeekUnavailable_ThrowsForRetry()
    {
        // Transient failure: throw so MassTransit retries. A briefly-between-
        // seasons sport will eventually DLQ via retry policy, at which point
        // a human re-enqueues via MatchupScheduler.
        var groupId = await SeedGroup(startsOn: null);
        _seasonClientMock
            .Setup(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<CanonicalSeasonWeekDto>(null!));

        var sut = Mocker.CreateInstance<PickemGroupCreatedHandler>();
        var context = ConsumeContextFor(new PickemGroupCreated(
            groupId, null, Sport.BaseballMlb, 2026, Guid.NewGuid(), Guid.NewGuid()));

        var act = async () => await sut.Consume(context);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _backgroundJobsMock.Verify(
            x => x.Enqueue<IScheduleGroupWeekMatchups>(It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()),
            Times.Never);
    }

    private async Task<Guid> SeedGroup(DateTime? startsOn)
    {
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Sport = Sport.BaseballMlb,
            League = League.MLB,
            StartsOn = startsOn,
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();
        return group.Id;
    }

    private static CanonicalSeasonWeekDto NewCurrentWeek() => new()
    {
        Id = Guid.NewGuid(),
        SeasonId = Guid.NewGuid(),
        SeasonYear = 2026,
        WeekNumber = 9,
        SeasonPhase = "regularseason",
        IsNonStandardWeek = false,
    };

    private void SetupCurrentWeek(CanonicalSeasonWeekDto week)
    {
        _seasonClientMock
            .Setup(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<CanonicalSeasonWeekDto>(week));
    }

    private static ConsumeContext<PickemGroupCreated> ConsumeContextFor(PickemGroupCreated message) =>
        Mock.Of<ConsumeContext<PickemGroupCreated>>(ctx => ctx.Message == message);
}
