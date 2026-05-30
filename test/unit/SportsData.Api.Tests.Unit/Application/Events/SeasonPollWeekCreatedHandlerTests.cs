using FluentAssertions;

using MassTransit;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Events;
using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Seasons;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Events;

/// <summary>
/// Pins the rank-poll refresh trigger. The consumer queries for active
/// leagues that:
///   • match the event's sport
///   • have a non-null RankingFilter (other leagues don't care about polls)
///   • are not deactivated
///   • have a window that overlaps the affected SeasonWeek
///   • already have a PickemGroupWeek for that SeasonWeekId
/// and enqueues a refresh-variant <see cref="ScheduleGroupWeekMatchupsCommand"/>
/// for each match.
/// </summary>
public class SeasonPollWeekCreatedHandlerTests : ApiTestBase<SeasonPollWeekCreatedHandler>
{
    private static readonly DateTime FixedNow = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekStart = new(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekEnd = new(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IProvideBackgroundJobs> _backgroundJobsMock;

    public SeasonPollWeekCreatedHandlerTests()
    {
        _backgroundJobsMock = Mocker.GetMock<IProvideBackgroundJobs>();
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }

    [Fact]
    public async Task NullSeasonWeekId_LogsAndReturns_NoEnqueue()
    {
        // Preseason/postseason "headline" polls don't map to a pickable
        // week — handler short-circuits before querying.
        var evt = NewEvent(seasonWeekId: null);
        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedHandler>();

        await sut.Consume(ConsumeContextFor(evt));

        VerifyEnqueueCount(0);
    }

    [Fact]
    public async Task MissingWeekDateBounds_LogsAndReturns_NoEnqueue()
    {
        // Defensive: producer always populates dates when SeasonWeekId is
        // set, but if it ever doesn't we can't overlap-test windows.
        var evt = NewEvent(seasonWeekId: Guid.NewGuid(), weekStart: null, weekEnd: null);
        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedHandler>();

        await sut.Consume(ConsumeContextFor(evt));

        VerifyEnqueueCount(0);
    }

    [Fact]
    public async Task NoMatchingLeagues_LogsAndReturns_NoEnqueue()
    {
        // Sport matches but no leagues use RankingFilter.
        var seasonWeekId = Guid.NewGuid();
        await SeedLeagueAndWeek(
            sport: Sport.FootballNcaa,
            rankingFilter: null,
            startsOn: WeekStart.AddDays(-30),
            endsOn: WeekEnd.AddDays(30),
            seasonWeekId: seasonWeekId,
            deactivatedUtc: null);

        var evt = NewEvent(seasonWeekId: seasonWeekId);
        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedHandler>();

        await sut.Consume(ConsumeContextFor(evt));

        VerifyEnqueueCount(0);
    }

    [Fact]
    public async Task MatchingLeague_EnqueuesRefreshCommand()
    {
        // Happy path: NCAAFB + TOP25 league whose window covers the week.
        var seasonWeekId = Guid.NewGuid();
        var groupId = await SeedLeagueAndWeek(
            sport: Sport.FootballNcaa,
            rankingFilter: TeamRankingFilter.AP_TOP_25,
            startsOn: WeekStart.AddDays(-30),
            endsOn: WeekEnd.AddDays(30),
            seasonWeekId: seasonWeekId,
            deactivatedUtc: null);

        ScheduleGroupWeekMatchupsCommand? captured = null;
        _backgroundJobsMock
            .Setup(x => x.Enqueue<IScheduleGroupWeekMatchups>(
                It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()))
            .Callback<Expression<Func<IScheduleGroupWeekMatchups, Task>>>(expr =>
                captured = ExtractCommand(expr))
            .Returns("job-id");

        var evt = NewEvent(seasonWeekId: seasonWeekId);
        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedHandler>();

        await sut.Consume(ConsumeContextFor(evt));

        VerifyEnqueueCount(1);
        captured.Should().NotBeNull();
        captured!.GroupId.Should().Be(groupId);
        captured.SeasonWeekId.Should().Be(seasonWeekId);
        captured.IsRefresh.Should().BeTrue();
        captured.CorrelationId.Should().Be(evt.CorrelationId);
    }

    [Fact]
    public async Task DeactivatedLeague_Skipped()
    {
        var seasonWeekId = Guid.NewGuid();
        await SeedLeagueAndWeek(
            sport: Sport.FootballNcaa,
            rankingFilter: TeamRankingFilter.AP_TOP_25,
            startsOn: WeekStart.AddDays(-30),
            endsOn: WeekEnd.AddDays(30),
            seasonWeekId: seasonWeekId,
            deactivatedUtc: FixedNow);

        var evt = NewEvent(seasonWeekId: seasonWeekId);
        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedHandler>();

        await sut.Consume(ConsumeContextFor(evt));

        VerifyEnqueueCount(0);
    }

    [Fact]
    public async Task LeagueWindowDoesNotOverlap_Skipped()
    {
        // Window ends before the affected week starts.
        var seasonWeekId = Guid.NewGuid();
        await SeedLeagueAndWeek(
            sport: Sport.FootballNcaa,
            rankingFilter: TeamRankingFilter.AP_TOP_25,
            startsOn: WeekStart.AddDays(-60),
            endsOn: WeekStart.AddDays(-1),
            seasonWeekId: seasonWeekId,
            deactivatedUtc: null);

        var evt = NewEvent(seasonWeekId: seasonWeekId);
        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedHandler>();

        await sut.Consume(ConsumeContextFor(evt));

        VerifyEnqueueCount(0);
    }

    [Fact]
    public async Task WrongSport_Skipped()
    {
        // NCAAFB poll lands; MLB league with TOP25 (hypothetical) shouldn't
        // get woken up by an NCAAFB poll.
        var seasonWeekId = Guid.NewGuid();
        await SeedLeagueAndWeek(
            sport: Sport.BaseballMlb,
            rankingFilter: TeamRankingFilter.AP_TOP_25,
            startsOn: WeekStart.AddDays(-30),
            endsOn: WeekEnd.AddDays(30),
            seasonWeekId: seasonWeekId,
            deactivatedUtc: null);

        var evt = NewEvent(seasonWeekId: seasonWeekId, sport: Sport.FootballNcaa);
        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedHandler>();

        await sut.Consume(ConsumeContextFor(evt));

        VerifyEnqueueCount(0);
    }

    [Fact]
    public async Task NoShellForSeasonWeek_Skipped()
    {
        // League matches everything but doesn't have a PickemGroupWeek for
        // the affected SeasonWeek yet (e.g. it's a future window that the
        // creation-time bootstrap or daily scheduler hasn't reached). Nothing
        // to refresh; the shell will be created when its own conditions are
        // met, and the next refresh trigger after that will populate it.
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "No-Shell League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            RankingFilter = TeamRankingFilter.AP_TOP_25,
            CommissionerUserId = Guid.NewGuid(),
            StartsOn = WeekStart.AddDays(-30),
            EndsOn = WeekEnd.AddDays(30),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.Empty,
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();

        var evt = NewEvent(seasonWeekId: Guid.NewGuid());
        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedHandler>();

        await sut.Consume(ConsumeContextFor(evt));

        VerifyEnqueueCount(0);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private async Task<Guid> SeedLeagueAndWeek(
        Sport sport,
        TeamRankingFilter? rankingFilter,
        DateTime? startsOn,
        DateTime? endsOn,
        Guid seasonWeekId,
        DateTime? deactivatedUtc)
    {
        var groupId = Guid.NewGuid();
        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test",
            Sport = sport,
            League = sport == Sport.BaseballMlb ? League.MLB : League.NCAAF,
            RankingFilter = rankingFilter,
            CommissionerUserId = Guid.NewGuid(),
            StartsOn = startsOn,
            EndsOn = endsOn,
            DeactivatedUtc = deactivatedUtc,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.Empty,
        };
        await DataContext.PickemGroups.AddAsync(group);

        var groupWeek = new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonWeekId = seasonWeekId,
            SeasonYear = 2026,
            SeasonWeek = 1,
            AreMatchupsGenerated = true,
            IsNonStandardWeek = false,
        };
        await DataContext.PickemGroupWeeks.AddAsync(groupWeek);
        await DataContext.SaveChangesAsync();

        return groupId;
    }

    private static SeasonPollWeekCreated NewEvent(
        Guid? seasonWeekId,
        DateTime? weekStart = null,
        DateTime? weekEnd = null,
        Sport sport = Sport.FootballNcaa) =>
        new(
            SeasonPollWeekId: Guid.NewGuid(),
            SeasonPollId: Guid.NewGuid(),
            SeasonWeekId: seasonWeekId,
            SeasonWeekStartDate: seasonWeekId.HasValue ? (weekStart ?? WeekStart) : null,
            SeasonWeekEndDate: seasonWeekId.HasValue ? (weekEnd ?? WeekEnd) : null,
            SeasonYear: 2026,
            PollSlug: "ap-top-25",
            Ref: null,
            Sport: sport,
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid());

    private static ConsumeContext<SeasonPollWeekCreated> ConsumeContextFor(SeasonPollWeekCreated message) =>
        Mock.Of<ConsumeContext<SeasonPollWeekCreated>>(ctx => ctx.Message == message);

    private void VerifyEnqueueCount(int expected)
    {
        _backgroundJobsMock.Verify(
            x => x.Enqueue<IScheduleGroupWeekMatchups>(
                It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()),
            Times.Exactly(expected));
    }

    private static ScheduleGroupWeekMatchupsCommand? ExtractCommand(
        Expression<Func<IScheduleGroupWeekMatchups, Task>> expr)
    {
        if (expr.Body is not MethodCallExpression call) return null;
        var arg = call.Arguments.FirstOrDefault();
        if (arg is null) return null;

        var lambda = Expression.Lambda<Func<ScheduleGroupWeekMatchupsCommand>>(arg).Compile();
        return lambda.Invoke();
    }
}
