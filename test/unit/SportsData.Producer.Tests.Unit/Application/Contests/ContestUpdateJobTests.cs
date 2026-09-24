#nullable enable

using System.Linq.Expressions;

using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests;

/// <summary>
/// The daily job scoped itself to the current season week. Its last run
/// inside an NCAAFB week (00:00 UTC Sunday, 20:00 Eastern Saturday) lands in
/// the middle of the evening slate; the week rolls over at 07:00 UTC; the
/// next run cannot see Saturday's unfinished games. Non-league games have no
/// stream row, so nothing else re-sources them: 68 games from weeks 1-3 sat
/// at STATUS_IN_PROGRESS for up to twelve days (2026-09-24). These pin the
/// stranded scope and its bounds.
/// </summary>
public class ContestUpdateJobTests : ProducerTestBase<ContestUpdateJob<FootballDataContext>>
{
    // Monday 00:00 UTC, 2026-09-21: the first run after week 3 rolled into week 4.
    private static readonly DateTime Now = new(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);

    private readonly List<Guid> _enqueued = [];
    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly Guid _phaseId = Guid.NewGuid();
    private Guid _week3Id;
    private Guid _week4Id;

    public ContestUpdateJobTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
        Mocker.GetMock<IAppMode>().SetupGet(x => x.CurrentSport).Returns(Sport.FootballNcaa);

        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue(It.IsAny<Expression<Func<IUpdateContests, Task>>>()))
            .Returns("job")
            .Callback<Expression<Func<IUpdateContests, Task>>>(e =>
            {
                var call = (MethodCallExpression)e.Body;
                var cmd = (UpdateContestCommand)Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke()!;
                _enqueued.Add(cmd.ContestId);
            });
    }

    private async Task SeedWeeksAsync()
    {
        await FootballDataContext.Seasons.AddAsync(new Season
        {
            Id = _seasonId, Year = 2026, Name = "2026",
            StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2027, 1, 31),
            CreatedUtc = Now, CreatedBy = Guid.Empty
        });
        _week3Id = Guid.NewGuid();
        _week4Id = Guid.NewGuid();
        // Real 2026 NCAAFB boundaries: weeks roll at 07:00 UTC Sunday.
        await FootballDataContext.SeasonWeeks.AddRangeAsync(
            new SeasonWeek
            {
                Id = _week3Id, SeasonId = _seasonId, SeasonPhaseId = _phaseId, Number = 3,
                StartDate = new DateTime(2026, 9, 13, 7, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 9, 20, 6, 59, 0, DateTimeKind.Utc),
                CreatedUtc = Now, CreatedBy = Guid.Empty
            },
            new SeasonWeek
            {
                Id = _week4Id, SeasonId = _seasonId, SeasonPhaseId = _phaseId, Number = 4,
                StartDate = new DateTime(2026, 9, 20, 7, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 9, 27, 6, 59, 0, DateTimeKind.Utc),
                CreatedUtc = Now, CreatedBy = Guid.Empty
            });
        await FootballDataContext.SaveChangesAsync();
    }

    private async Task<Guid> SeedContestAsync(Guid weekId, DateTime start, bool finalized = false, bool cancelled = false)
    {
        var contest = Fixture.Build<FootballContest>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SeasonWeekId, weekId)
            .With(x => x.StartDateUtc, start)
            .With(x => x.FinalizedUtc, finalized ? start.AddHours(4) : (DateTime?)null)
            .With(x => x.CancelledUtc, cancelled ? start : (DateTime?)null)
            .With(x => x.HomeTeamFranchiseSeasonId, Guid.NewGuid())
            .With(x => x.AwayTeamFranchiseSeasonId, Guid.NewGuid())
            .Without(x => x.HomeTeamFranchiseSeason).Without(x => x.AwayTeamFranchiseSeason)
            // A fixture-generated SeasonWeek navigation would make EF overwrite
            // the SeasonWeekId set above with the random week's id on Add.
            .Without(x => x.SeasonWeek).Without(x => x.Venue)
            .Without(x => x.Links).Without(x => x.ExternalIds).Without(x => x.Competitions)
            .Create();
        await FootballDataContext.Contests.AddAsync(contest);
        await FootballDataContext.SaveChangesAsync();
        return contest.Id;
    }

    [Fact]
    public async Task Execute_ReSourcesLastWeeksUnfinishedEveningGames_AlongsideTheCurrentWeek()
    {
        await SeedWeeksAsync();
        // Ohio @ South Alabama, Saturday 19:00 Eastern (23:00 UTC), week 3: kicked
        // off 25h before this run and never finalized.
        var stranded = await SeedContestAsync(_week3Id, new DateTime(2026, 9, 19, 23, 0, 0, DateTimeKind.Utc));
        // A week-4 game later this week: current-week scope, unchanged behaviour.
        var upcoming = await SeedContestAsync(_week4Id, new DateTime(2026, 9, 26, 16, 0, 0, DateTimeKind.Utc));

        await Mocker.CreateInstance<ContestUpdateJob<FootballDataContext>>().ExecuteAsync();

        _enqueued.Should().BeEquivalentTo([stranded, upcoming]);
    }

    [Fact]
    public async Task Execute_LeavesFinalizedAndCancelledGamesFromLastWeekAlone()
    {
        await SeedWeeksAsync();
        await SeedContestAsync(_week3Id, new DateTime(2026, 9, 19, 16, 0, 0, DateTimeKind.Utc), finalized: true);
        await SeedContestAsync(_week3Id, new DateTime(2026, 9, 19, 16, 0, 0, DateTimeKind.Utc), cancelled: true);

        await Mocker.CreateInstance<ContestUpdateJob<FootballDataContext>>().ExecuteAsync();

        _enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_StrandedScopeIsBounded_ByAgeAndByWindow()
    {
        await SeedWeeksAsync();
        // Kicked off 2h ago in last week's bucket (odd, but possible around the
        // rollover): still legitimately live, not stranded.
        await SeedContestAsync(_week3Id, Now.AddHours(-2));
        // Older than the window: left to the manual season refresh.
        await SeedContestAsync(_week3Id, Now.AddDays(-15));
        // Inside both bounds.
        var stranded = await SeedContestAsync(_week3Id, Now.AddDays(-13));

        await Mocker.CreateInstance<ContestUpdateJob<FootballDataContext>>().ExecuteAsync();

        _enqueued.Should().Equal(stranded);
    }

    [Fact]
    public async Task Execute_DoesNotDoubleEnqueue_AGameThatMatchesBothScopes()
    {
        await SeedWeeksAsync();
        // Week-4 game that already kicked off 4h ago: current-week scope AND
        // it satisfies the stranded bounds except for the week check.
        var live = await SeedContestAsync(_week4Id, Now.AddHours(-4));

        await Mocker.CreateInstance<ContestUpdateJob<FootballDataContext>>().ExecuteAsync();

        _enqueued.Should().Equal(live);
    }

    [Fact]
    public async Task Execute_WithoutACurrentWeek_DoesNothing()
    {
        // Off-season: no week contains "now". Existing behaviour preserved.
        await FootballDataContext.SaveChangesAsync();

        await Mocker.CreateInstance<ContestUpdateJob<FootballDataContext>>().ExecuteAsync();

        _enqueued.Should().BeEmpty();
    }
}
