#nullable enable

using System.Linq.Expressions;

using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Application.Competitions.Commands.CalculateCompetitionMetrics;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

/// <summary>
/// The audit's job is "per-game metrics are current". Its original scope,
/// competitions with no rows, treated a row computed from zero plays as done
/// forever: 139 games this season sat at all-zero for the whole week after
/// their plays arrived. These pin the second scope (null-hash rows whose
/// competition has plays now), the season-aggregate follow-up, and the two
/// things the audit must NOT touch.
/// </summary>
public class FootballCompetitionMetricsAuditJobTests : ProducerTestBase<FootballCompetitionMetricsAuditJob>
{
    private static readonly DateTime Now = new(2026, 9, 21, 7, 0, 0, DateTimeKind.Utc);

    private readonly List<Guid> _enqueuedCompetitions = [];
    private readonly List<(Guid FranchiseSeasonId, int SeasonYear, TimeSpan Delay)> _scheduledSeasons = [];

    public FootballCompetitionMetricsAuditJobTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);

        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue(It.IsAny<Expression<Func<ICalculateCompetitionMetricsCommandHandler, Task>>>()))
            .Callback<Expression<Func<ICalculateCompetitionMetricsCommandHandler, Task>>>(e =>
                _enqueuedCompetitions.Add(FirstArgument<CalculateCompetitionMetricsCommand>(e).CompetitionId));

        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Schedule(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>(), It.IsAny<TimeSpan>()))
            .Callback<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>, TimeSpan>((e, delay) =>
            {
                var cmd = FirstArgument<CalculateFranchiseSeasonMetricsCommand>(e);
                _scheduledSeasons.Add((cmd.FranchiseSeasonId, cmd.SeasonYear, delay));
            });
    }

    private static T FirstArgument<T>(LambdaExpression captured)
    {
        var call = (MethodCallExpression)captured.Body;
        return (T)Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke()!;
    }

    private async Task<(Guid CompetitionId, Guid HomeId, Guid AwayId)> SeedGameAsync(
        DateTime date, string? inputsHash, bool withRows, bool withPlays, int seasonYear = 2026)
    {
        var homeId = Guid.NewGuid();
        var awayId = Guid.NewGuid();
        var homeFs = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, homeId).With(x => x.FranchiseId, Guid.NewGuid())
            .With(x => x.SeasonYear, seasonYear).Without(x => x.ExternalIds).Create();
        var awayFs = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, awayId).With(x => x.FranchiseId, Guid.NewGuid())
            .With(x => x.SeasonYear, seasonYear).Without(x => x.ExternalIds).Create();
        await FootballDataContext.FranchiseSeasons.AddRangeAsync(homeFs, awayFs);

        var contest = Fixture.Build<FootballContest>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.HomeTeamFranchiseSeasonId, homeId)
            .With(x => x.AwayTeamFranchiseSeasonId, awayId)
            .With(x => x.HomeTeamFranchiseSeason, homeFs)
            .With(x => x.AwayTeamFranchiseSeason, awayFs)
            .Without(x => x.Links).Without(x => x.ExternalIds).Without(x => x.Competitions)
            .Create();
        await FootballDataContext.Contests.AddAsync(contest);

        var competitionId = Guid.NewGuid();
        var competition = Fixture.Build<FootballCompetition>()
            .With(x => x.Id, competitionId)
            .With(x => x.ContestId, contest.Id)
            .With(x => x.Contest, contest)
            .With(x => x.Date, date)
            .Without(x => x.Plays).Without(x => x.Drives).Without(x => x.Metrics)
            .Without(x => x.Links).Without(x => x.ExternalIds)
            .Create();
        await FootballDataContext.Competitions.AddAsync(competition);

        if (withRows)
        {
            foreach (var fsId in new[] { awayId, homeId })
            {
                await FootballDataContext.CompetitionMetrics.AddAsync(new CompetitionMetric
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    FranchiseSeasonId = fsId,
                    Season = seasonYear,
                    InputsHash = inputsHash,
                    FormulaVersion = MetricFormula.Version,
                    ComputedUtc = date.AddHours(5),
                    CreatedUtc = date.AddHours(5),
                    CreatedBy = Guid.Empty
                });
            }
        }

        if (withPlays)
        {
            await FootballDataContext.CompetitionPlays.AddAsync(new FootballCompetitionPlay
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                DriveId = Guid.NewGuid(),
                EspnId = "p1",
                SequenceNumber = "1",
                TypeId = "0",
                Text = "synthetic",
                StartFranchiseSeasonId = homeId,
                CreatedUtc = date.AddHours(30),
                CreatedBy = Guid.Empty
            });
        }

        await FootballDataContext.SaveChangesAsync();
        return (competitionId, homeId, awayId);
    }

    [Fact]
    public async Task Execute_RecomputesAGame_WhoseRowsWereComputedFromNoPlays_AndNowHasPlays()
    {
        // Colorado @ Northwestern, 2026-09-19: rows written Sunday 07:00 UTC
        // from zero plays (InputsHash null), plays landed Sunday afternoon.
        var (competitionId, homeId, awayId) = await SeedGameAsync(
            Now.AddDays(-2), inputsHash: null, withRows: true, withPlays: true);

        await Mocker.CreateInstance<FootballCompetitionMetricsAuditJob>().ExecuteAsync();

        _enqueuedCompetitions.Should().Equal(competitionId);
        _scheduledSeasons.Select(s => s.FranchiseSeasonId).Should().BeEquivalentTo([homeId, awayId]);
        _scheduledSeasons.Should().OnlyContain(s => s.SeasonYear == 2026 && s.Delay == FootballCompetitionMetricsAuditJob.SeasonRecomputeDelay);
    }

    [Fact]
    public async Task Execute_StillRecomputesAGame_WithNoRowsAtAll()
    {
        var (competitionId, _, _) = await SeedGameAsync(
            Now.AddDays(-1), inputsHash: null, withRows: false, withPlays: true);

        await Mocker.CreateInstance<FootballCompetitionMetricsAuditJob>().ExecuteAsync();

        _enqueuedCompetitions.Should().Equal(competitionId);
        _scheduledSeasons.Should().HaveCount(2);
    }

    [Fact]
    public async Task Execute_LeavesAloneAGame_WhoseRowsWereComputedFromNoPlays_AndStillHasNone()
    {
        // FCS opponents with no play-by-play source: recomputing would write
        // the same zeros and churn 346 games a night for nothing.
        await SeedGameAsync(Now.AddDays(-3), inputsHash: null, withRows: true, withPlays: false);

        await Mocker.CreateInstance<FootballCompetitionMetricsAuditJob>().ExecuteAsync();

        _enqueuedCompetitions.Should().BeEmpty();
        _scheduledSeasons.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_LeavesAloneAGame_WhoseRowsCarryAnInputsHash()
    {
        // Computed from real plays; the hash is the calculator's receipt.
        await SeedGameAsync(Now.AddDays(-3), inputsHash: "abc123", withRows: true, withPlays: true);

        await Mocker.CreateInstance<FootballCompetitionMetricsAuditJob>().ExecuteAsync();

        _enqueuedCompetitions.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_IgnoresGames_LessThanThreeHoursOld()
    {
        await SeedGameAsync(Now.AddHours(-1), inputsHash: null, withRows: false, withPlays: false);
        await SeedGameAsync(Now.AddHours(-2), inputsHash: null, withRows: true, withPlays: true);

        await Mocker.CreateInstance<FootballCompetitionMetricsAuditJob>().ExecuteAsync();

        _enqueuedCompetitions.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_SchedulesEachAffectedFranchiseSeasonOnce_AcrossGames()
    {
        // Two stale games sharing a team: the shared team's aggregate is
        // scheduled once, not per game.
        var (c1, home1, away1) = await SeedGameAsync(Now.AddDays(-2), null, withRows: true, withPlays: true);
        var (c2, _, _) = await SeedGameAsync(Now.AddDays(-9), null, withRows: false, withPlays: true);

        // Point game 2's contest at game 1's home team.
        var contest2 = await FootballDataContext.Contests.FindAsync(
            (await FootballDataContext.Competitions.FindAsync(c2))!.ContestId);
        contest2!.AwayTeamFranchiseSeasonId = home1;
        await FootballDataContext.SaveChangesAsync();

        await Mocker.CreateInstance<FootballCompetitionMetricsAuditJob>().ExecuteAsync();

        _enqueuedCompetitions.Should().BeEquivalentTo([c1, c2]);
        _scheduledSeasons.Select(s => s.FranchiseSeasonId).Should().OnlyHaveUniqueItems();
        _scheduledSeasons.Select(s => s.FranchiseSeasonId).Should().Contain([home1, away1]);
        _scheduledSeasons.Should().HaveCount(3);
    }
}
