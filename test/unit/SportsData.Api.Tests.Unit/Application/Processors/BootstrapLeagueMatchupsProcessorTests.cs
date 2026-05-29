using FluentAssertions;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Processors;

/// <summary>
/// Pins the dispatch behavior on the creation-time orchestrator. Each row in
/// docs/league-creation-matrix.md collapses to one of these test cases:
///   • Full-season → GetCurrentSeasonWeek → 1 enqueue (rows 1, 2, 14).
///   • Windowed   → GetSeasonWeeksOverlapping → N enqueues (rows 7-13).
///   • Empty range result → 0 enqueues (row 11).
///   • Group not found → 0 enqueues, no exception.
///   • Endpoint failure → throw (transient).
///   • Partial window (EndsOn=null) → 365-day cap applied to `to`.
/// </summary>
public class BootstrapLeagueMatchupsProcessorTests : ApiTestBase<BootstrapLeagueMatchupsProcessor>
{
    private static readonly DateTime FixedNow = new(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ISeasonClientFactory> _seasonClientFactoryMock;
    private readonly Mock<IProvideSeasons> _seasonClientMock;
    private readonly Mock<IProvideBackgroundJobs> _backgroundJobsMock;

    public BootstrapLeagueMatchupsProcessorTests()
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
        var sut = Mocker.CreateInstance<BootstrapLeagueMatchupsProcessor>();
        var command = new BootstrapLeagueMatchupsCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = async () => await sut.Process(command);

        await act.Should().NotThrowAsync();
        VerifyNoEnqueues();
    }

    [Fact]
    public async Task FullSeasonLeague_HitsCurrentWeekEndpoint_AndEnqueuesOne()
    {
        // Row 1: StartsOn=null, EndsOn=null → GetCurrentSeasonWeek path.
        var groupId = await SeedGroup(startsOn: null, endsOn: null);
        var week = SeasonWeek(weekNumber: 5);
        _seasonClientMock
            .Setup(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<CanonicalSeasonWeekDto>(week));

        var sut = Mocker.CreateInstance<BootstrapLeagueMatchupsProcessor>();
        var command = new BootstrapLeagueMatchupsCommand(groupId, Guid.NewGuid());

        await sut.Process(command);

        _seasonClientMock.Verify(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()), Times.Once);
        _seasonClientMock.Verify(x => x.GetSeasonWeeksOverlapping(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyEnqueueCount(1);
    }

    [Fact]
    public async Task WindowedLeague_HitsDateRangeEndpoint_AndEnqueuesPerWeek()
    {
        // Rows 12/13: windowed multi-week. 3 SeasonWeeks → 3 enqueues.
        var startsOn = FixedNow.AddDays(30);
        var endsOn = FixedNow.AddDays(50);
        var groupId = await SeedGroup(startsOn: startsOn, endsOn: endsOn);

        _seasonClientMock
            .Setup(x => x.GetSeasonWeeksOverlapping(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CanonicalSeasonWeekDto>>(new List<CanonicalSeasonWeekDto>
            {
                SeasonWeek(weekNumber: 1),
                SeasonWeek(weekNumber: 2),
                SeasonWeek(weekNumber: 3),
            }));

        var sut = Mocker.CreateInstance<BootstrapLeagueMatchupsProcessor>();
        var command = new BootstrapLeagueMatchupsCommand(groupId, Guid.NewGuid());

        await sut.Process(command);

        _seasonClientMock.Verify(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()), Times.Never);
        _seasonClientMock.Verify(x => x.GetSeasonWeeksOverlapping(
            startsOn, endsOn, It.IsAny<CancellationToken>()), Times.Once);
        VerifyEnqueueCount(3);
    }

    [Fact]
    public async Task WindowedLeague_PartialWindow_StartsOnOnly_AppliesPartialWindowCap()
    {
        // Row 6: StartsOn=future, EndsOn=null. `to` must be capped at
        // StartsOn + 365d per DP-3.
        var startsOn = FixedNow.AddDays(30);
        var groupId = await SeedGroup(startsOn: startsOn, endsOn: null);

        _seasonClientMock
            .Setup(x => x.GetSeasonWeeksOverlapping(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CanonicalSeasonWeekDto>>([SeasonWeek(weekNumber: 1)]));

        var sut = Mocker.CreateInstance<BootstrapLeagueMatchupsProcessor>();
        var command = new BootstrapLeagueMatchupsCommand(groupId, Guid.NewGuid());

        await sut.Process(command);

        _seasonClientMock.Verify(
            x => x.GetSeasonWeeksOverlapping(
                startsOn,
                startsOn.AddDays(365),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WindowedLeague_PartialWindow_EndsOnOnly_FromBoundDefaultsToNow()
    {
        // Row 4: StartsOn=null, EndsOn=future. `from` defaults to now.
        var endsOn = FixedNow.AddDays(60);
        var groupId = await SeedGroup(startsOn: null, endsOn: endsOn);

        _seasonClientMock
            .Setup(x => x.GetSeasonWeeksOverlapping(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CanonicalSeasonWeekDto>>([SeasonWeek(weekNumber: 1)]));

        var sut = Mocker.CreateInstance<BootstrapLeagueMatchupsProcessor>();
        var command = new BootstrapLeagueMatchupsCommand(groupId, Guid.NewGuid());

        await sut.Process(command);

        _seasonClientMock.Verify(
            x => x.GetSeasonWeeksOverlapping(
                FixedNow,
                endsOn,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvertedDateRange_LogsAndReturns_NoEndpointCall_NoEnqueues()
    {
        // Permanent input error: window resolved to from > to. The PR-B
        // validator blocks this at creation but it can surface here if
        // EndsOn was set, StartsOn was null, and the clock rolled past
        // EndsOn before the Hangfire job ran. Verify the processor
        // short-circuits without calling the date-range endpoint and
        // without throwing (which would burn Hangfire retry budget on a
        // state that can't be fixed by retrying).
        var groupId = await SeedGroup(startsOn: null, endsOn: FixedNow.AddDays(-1));

        var sut = Mocker.CreateInstance<BootstrapLeagueMatchupsProcessor>();
        var command = new BootstrapLeagueMatchupsCommand(groupId, Guid.NewGuid());

        var act = async () => await sut.Process(command);

        await act.Should().NotThrowAsync();
        _seasonClientMock.Verify(
            x => x.GetSeasonWeeksOverlapping(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoEnqueues();
    }

    [Fact]
    public async Task EmptyDateRangeResult_LogsAndReturns_NoEnqueues()
    {
        // Row 11: very-far-future league whose SeasonWeeks aren't sourced
        // yet. Endpoint returns empty Success; we log + return; daily
        // scheduler picks up later.
        var groupId = await SeedGroup(startsOn: FixedNow.AddYears(1), endsOn: FixedNow.AddYears(1).AddDays(1));
        _seasonClientMock
            .Setup(x => x.GetSeasonWeeksOverlapping(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CanonicalSeasonWeekDto>>([]));

        var sut = Mocker.CreateInstance<BootstrapLeagueMatchupsProcessor>();
        var command = new BootstrapLeagueMatchupsCommand(groupId, Guid.NewGuid());

        var act = async () => await sut.Process(command);

        await act.Should().NotThrowAsync();
        VerifyNoEnqueues();
    }

    [Fact]
    public async Task FullSeasonLeague_CurrentWeekFailure_Throws()
    {
        // Row 2/3: current-week lookup fails. Throw so Hangfire retries.
        var groupId = await SeedGroup(startsOn: null, endsOn: null);
        _seasonClientMock
            .Setup(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<CanonicalSeasonWeekDto>(
                default!, ResultStatus.NotFound, new List<ValidationFailure>()));

        var sut = Mocker.CreateInstance<BootstrapLeagueMatchupsProcessor>();
        var command = new BootstrapLeagueMatchupsCommand(groupId, Guid.NewGuid());

        var act = async () => await sut.Process(command);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyNoEnqueues();
    }

    [Fact]
    public async Task WindowedLeague_DateRangeFailure_Throws()
    {
        // Date-range endpoint failure is also transient; Hangfire retries.
        var groupId = await SeedGroup(startsOn: FixedNow.AddDays(10), endsOn: FixedNow.AddDays(20));
        _seasonClientMock
            .Setup(x => x.GetSeasonWeeksOverlapping(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<List<CanonicalSeasonWeekDto>>(
                default!, ResultStatus.NotFound, new List<ValidationFailure>()));

        var sut = Mocker.CreateInstance<BootstrapLeagueMatchupsProcessor>();
        var command = new BootstrapLeagueMatchupsCommand(groupId, Guid.NewGuid());

        var act = async () => await sut.Process(command);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyNoEnqueues();
    }

    [Fact]
    public async Task EnqueuedCommandsCarryCorrelationId()
    {
        // The CorrelationId on the bootstrap command threads through to each
        // per-week command so the whole chain shows up under one trace.
        var groupId = await SeedGroup(startsOn: FixedNow.AddDays(10), endsOn: FixedNow.AddDays(20));
        var correlationId = Guid.NewGuid();

        _seasonClientMock
            .Setup(x => x.GetSeasonWeeksOverlapping(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CanonicalSeasonWeekDto>>([SeasonWeek(weekNumber: 1)]));

        ScheduleGroupWeekMatchupsCommand? captured = null;
        _backgroundJobsMock
            .Setup(x => x.Enqueue<IScheduleGroupWeekMatchups>(
                It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()))
            .Callback<Expression<Func<IScheduleGroupWeekMatchups, Task>>>(expr =>
            {
                captured = ExtractCommand(expr);
            })
            .Returns("job-id");

        var sut = Mocker.CreateInstance<BootstrapLeagueMatchupsProcessor>();
        var command = new BootstrapLeagueMatchupsCommand(groupId, correlationId);

        await sut.Process(command);

        captured.Should().NotBeNull();
        captured!.CorrelationId.Should().Be(correlationId);
        captured.GroupId.Should().Be(groupId);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private async Task<Guid> SeedGroup(DateTime? startsOn, DateTime? endsOn)
    {
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Sport = Sport.BaseballMlb,
            League = League.MLB,
            StartsOn = startsOn,
            EndsOn = endsOn,
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();
        return group.Id;
    }

    private static CanonicalSeasonWeekDto SeasonWeek(int weekNumber) => new()
    {
        Id = Guid.NewGuid(),
        SeasonId = Guid.NewGuid(),
        SeasonYear = 2026,
        WeekNumber = weekNumber,
        SeasonPhase = "regularseason",
        IsNonStandardWeek = false,
    };

    private void VerifyEnqueueCount(int expected)
    {
        _backgroundJobsMock.Verify(
            x => x.Enqueue<IScheduleGroupWeekMatchups>(
                It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()),
            Times.Exactly(expected));
    }

    private void VerifyNoEnqueues() => VerifyEnqueueCount(0);

    /// <summary>
    /// Inspects the captured <c>p =&gt; p.Process(cmd)</c> expression tree
    /// to pull out the constructed command, so per-week command fields can
    /// be asserted (correlation id, week number, etc.).
    /// </summary>
    private static ScheduleGroupWeekMatchupsCommand? ExtractCommand(
        Expression<Func<IScheduleGroupWeekMatchups, Task>> expr)
    {
        if (expr.Body is not MethodCallExpression call) return null;
        var arg = call.Arguments.FirstOrDefault();
        if (arg is null) return null;

        // The argument is a closure over the local `perWeekCommand` variable —
        // compile + invoke to materialize the constructed record.
        var lambda = Expression.Lambda<Func<ScheduleGroupWeekMatchupsCommand>>(arg).Compile();
        return lambda.Invoke();
    }
}
