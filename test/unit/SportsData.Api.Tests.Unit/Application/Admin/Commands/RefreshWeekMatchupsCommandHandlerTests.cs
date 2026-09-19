using System.Linq.Expressions;

using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Api.Application.Admin.Commands.RefreshWeekMatchups;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Processing;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Commands;

/// <summary>
/// The week a caller names has to be the week that gets refreshed. Week NUMBERS
/// are ambiguous across phase AND sport — 2026 week 2 exists for NCAA football,
/// NFL preseason and NFL regular season at once — so these pin that the handler
/// reports the ambiguity with enough detail to act on rather than guessing.
/// Guessing wrong silently refreshes another sport's slate.
/// </summary>
public class RefreshWeekMatchupsCommandHandlerTests : ApiTestBase<RefreshWeekMatchupsCommandHandler>
{
    private static readonly DateTime Now = new(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);

    public RefreshWeekMatchupsCommandHandlerTests()
    {
        // The real validator: its job (one addressing form or the other) is
        // part of what these assert.
        Mocker.Use<IValidator<RefreshWeekMatchupsCommand>>(new RefreshWeekMatchupsCommandValidator());
    }

    [Fact]
    public async Task Execute_WithExplicitSeasonWeekId_EnqueuesARefreshPerLeague()
    {
        var seasonWeekId = Guid.NewGuid();
        var a = await SeedLeagueWeekAsync(seasonWeekId, Sport.FootballNcaa, phase: 2, week: 2);
        var b = await SeedLeagueWeekAsync(seasonWeekId, Sport.FootballNcaa, phase: 2, week: 2);

        var enqueued = CaptureEnqueues();

        var result = await Mocker.CreateInstance<RefreshWeekMatchupsCommandHandler>()
            .ExecuteAsync(new RefreshWeekMatchupsCommand { SeasonWeekId = seasonWeekId });

        result.IsSuccess.Should().BeTrue();
        result.Value.LeaguesQueued.Should().Be(2);
        enqueued.Should().HaveCount(2);
        // IsRefresh is the whole point: without it the processor short-circuits
        // on AreMatchupsGenerated and the week is never re-synced.
        enqueued.Should().OnlyContain(c => c.IsRefresh && c.SeasonWeekId == seasonWeekId);
        enqueued.Select(c => c.GroupId).Should().BeEquivalentTo([a, b]);
    }

    [Fact]
    public async Task Execute_WithYearAndWeek_ResolvesWhenOnlyOneWeekMatches()
    {
        var seasonWeekId = Guid.NewGuid();
        await SeedLeagueWeekAsync(seasonWeekId, Sport.FootballNcaa, phase: 2, week: 2);

        var enqueued = CaptureEnqueues();

        var result = await Mocker.CreateInstance<RefreshWeekMatchupsCommandHandler>()
            .ExecuteAsync(new RefreshWeekMatchupsCommand { SeasonYear = 2026, SeasonWeek = 2 });

        result.IsSuccess.Should().BeTrue();
        result.Value.SeasonWeekId.Should().Be(seasonWeekId);
        enqueued.Should().ContainSingle();
    }

    [Fact]
    public async Task Execute_WhenWeekNumberSpansSportsAndPhases_ReportsCandidatesInsteadOfGuessing()
    {
        var ncaaRegular = Guid.NewGuid();
        var nflPre = Guid.NewGuid();
        var nflRegular = Guid.NewGuid();
        await SeedLeagueWeekAsync(ncaaRegular, Sport.FootballNcaa, phase: 2, week: 2);
        await SeedLeagueWeekAsync(nflPre, Sport.FootballNfl, phase: 1, week: 2);
        await SeedLeagueWeekAsync(nflRegular, Sport.FootballNfl, phase: 2, week: 2);

        var enqueued = CaptureEnqueues();

        var result = await Mocker.CreateInstance<RefreshWeekMatchupsCommandHandler>()
            .ExecuteAsync(new RefreshWeekMatchupsCommand { SeasonYear = 2026, SeasonWeek = 2 });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        enqueued.Should().BeEmpty("refreshing the wrong sport's slate is worse than refusing");

        // Each candidate must be identifiable without a database round-trip:
        // bare guids are what produced a blind pick on 2026-09-18.
        var message = string.Join(" ", ((Failure<RefreshWeekMatchupsResponse>)result).Errors.Select(e => e.ErrorMessage));
        message.Should().Contain(ncaaRegular.ToString());
        message.Should().Contain(nflPre.ToString());
        message.Should().Contain(nflRegular.ToString());
        message.Should().Contain("FootballNcaa");
        message.Should().Contain("FootballNfl");
        message.Should().Contain("preseason");
    }

    [Fact]
    public async Task Execute_SportNarrowsAnOtherwiseAmbiguousWeek()
    {
        var ncaaRegular = Guid.NewGuid();
        await SeedLeagueWeekAsync(ncaaRegular, Sport.FootballNcaa, phase: 2, week: 2);
        await SeedLeagueWeekAsync(Guid.NewGuid(), Sport.FootballNfl, phase: 1, week: 2);
        await SeedLeagueWeekAsync(Guid.NewGuid(), Sport.FootballNfl, phase: 2, week: 2);

        var enqueued = CaptureEnqueues();

        var result = await Mocker.CreateInstance<RefreshWeekMatchupsCommandHandler>()
            .ExecuteAsync(new RefreshWeekMatchupsCommand
            {
                SeasonYear = 2026,
                SeasonWeek = 2,
                Sport = Sport.FootballNcaa
            });

        result.IsSuccess.Should().BeTrue();
        result.Value.SeasonWeekId.Should().Be(ncaaRegular);
        enqueued.Should().ContainSingle();
    }

    [Fact]
    public async Task Execute_WithNeitherAddressingForm_FailsValidation()
    {
        var result = await Mocker.CreateInstance<RefreshWeekMatchupsCommandHandler>()
            .ExecuteAsync(new RefreshWeekMatchupsCommand { SeasonYear = 2026 });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task Execute_WhenNoLeagueHoldsTheWeek_ReturnsNotFound()
    {
        var result = await Mocker.CreateInstance<RefreshWeekMatchupsCommandHandler>()
            .ExecuteAsync(new RefreshWeekMatchupsCommand { SeasonWeekId = Guid.NewGuid() });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Execute_SkipsDeactivatedLeagues()
    {
        var seasonWeekId = Guid.NewGuid();
        var active = await SeedLeagueWeekAsync(seasonWeekId, Sport.FootballNcaa, phase: 2, week: 2);
        await SeedLeagueWeekAsync(seasonWeekId, Sport.FootballNcaa, phase: 2, week: 2, deactivated: true);

        var enqueued = CaptureEnqueues();

        var result = await Mocker.CreateInstance<RefreshWeekMatchupsCommandHandler>()
            .ExecuteAsync(new RefreshWeekMatchupsCommand { SeasonWeekId = seasonWeekId });

        result.Value.LeaguesQueued.Should().Be(1);
        enqueued.Should().ContainSingle().Which.GroupId.Should().Be(active);
    }

    private List<ScheduleGroupWeekMatchupsCommand> CaptureEnqueues()
    {
        var captured = new List<ScheduleGroupWeekMatchupsCommand>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IScheduleGroupWeekMatchups>(
                It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()))
            .Callback<Expression<Func<IScheduleGroupWeekMatchups, Task>>>(expr =>
            {
                var call = (MethodCallExpression)expr.Body;
                var arg = Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke();
                captured.Add((ScheduleGroupWeekMatchupsCommand)arg!);
            });
        return captured;
    }

    private async Task<Guid> SeedLeagueWeekAsync(
        Guid seasonWeekId, Sport sport, int phase, int week, bool deactivated = false)
    {
        var groupId = Guid.NewGuid();

        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = groupId,
            Name = $"League {groupId:N}",
            Sport = sport,
            League = sport == Sport.FootballNcaa ? League.NCAAF : League.NFL,
            CommissionerUserId = Guid.NewGuid(),
            SeasonYear = 2026,
            DeactivatedUtc = deactivated ? Now.AddDays(-1) : null,
            CreatedUtc = Now.AddDays(-30),
            CreatedBy = Guid.Empty
        });

        DataContext.PickemGroupWeeks.Add(new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonWeekId = seasonWeekId,
            SeasonYear = 2026,
            SeasonWeek = week,
            SeasonPhaseTypeCode = phase,
            AreMatchupsGenerated = true,
            IsNonStandardWeek = false,
            CreatedUtc = Now.AddDays(-20),
            CreatedBy = Guid.Empty
        });

        await DataContext.SaveChangesAsync();
        return groupId;
    }
}
