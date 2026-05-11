using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Contests.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;

namespace SportsData.Producer.Application.Contests
{
    public interface IBaseballContestReplayService
    {
        Task ReplayContest(
            Guid contestId,
            Guid correlationId,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Replays a completed baseball contest by emitting the lifecycle
    /// event once and then a per-play <see cref="BaseballPlayCompleted"/>
    /// for each stored play. Used by the admin replay endpoints to drive
    /// the live UI through a known game without needing a real ESPN feed.
    /// Football has its own equivalent in <see cref="FootballContestReplayService"/>.
    ///
    /// Runner state still emits as defaults — the
    /// EventCompetitionSituation pipeline that backs it is deferred
    /// (Phase 3 of docs/baseball-live-data-plan.md). HalfInning, Outs,
    /// and the at-bat / pitcher display fields hydrate from the
    /// captured entity + participants per PR #310 + PR #311.
    /// </summary>
    public class BaseballContestReplayService : IBaseballContestReplayService
    {
        private readonly ILogger<BaseballContestReplayService> _logger;
        private readonly BaseballDataContext _dataContext;
        private readonly IEventBus _eventBus;

        public BaseballContestReplayService(
            ILogger<BaseballContestReplayService> logger,
            BaseballDataContext dataContext,
            IEventBus eventBus)
        {
            _logger = logger;
            _dataContext = dataContext;
            _eventBus = eventBus;
        }

        public async Task ReplayContest(
            Guid contestId,
            Guid correlationId,
            CancellationToken ct = default)
        {
            _logger.LogInformation(
                "BaseballReplay: starting. ContestId={ContestId}, CorrelationId={CorrelationId}",
                contestId, correlationId);

            var contest = await _dataContext.Contests
                .FirstOrDefaultAsync(c => c.Id == contestId, ct);

            if (contest is null)
            {
                _logger.LogError(
                    "BaseballReplay: contest not found. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId, correlationId);
                return;
            }

            _logger.LogInformation(
                "BaseballReplay: contest loaded. ContestId={ContestId}, Sport={Sport}, SeasonYear={SeasonYear}, FinalizedUtc={FinalizedUtc}, CorrelationId={CorrelationId}",
                contestId, contest.Sport, contest.SeasonYear, contest.FinalizedUtc, correlationId);

            var competition = await _dataContext.Competitions
                .FirstOrDefaultAsync(c => c.ContestId == contestId, ct);

            if (competition is null)
            {
                _logger.LogError(
                    "BaseballReplay: competition not found. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId, correlationId);
                return;
            }

            _logger.LogInformation(
                "BaseballReplay: competition loaded. CompetitionId={CompetitionId}, ContestId={ContestId}, CorrelationId={CorrelationId}",
                competition.Id, contestId, correlationId);

            var finalizedPrev = contest.FinalizedUtc;

            try
            {
                contest.FinalizedUtc = null;

                await _dataContext.SaveChangesAsync(ct);

                // Lifecycle bookend — Scheduled→InProgress. Sport-neutral.
                await _eventBus.Publish(new ContestStatusChanged(
                    contestId,
                    nameof(ContestStatus.InProgress),
                    null,
                    contest.Sport,
                    contest.SeasonYear,
                    correlationId,
                    CausationId.Producer.EventCompetitionStatusDocumentProcessor
                ), ct);

                await _dataContext.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "BaseballReplay: published ContestStatusChanged=InProgress. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId, correlationId);

                // Query CompetitionPlays directly off the DbSet rather
                // than the navigation collection — the navigation came
                // back empty in practice (no Include + no lazy-loading
                // proxies) even when stored plays exist.
                // Include Participants so the payload builder can read
                // PositionId off the in-memory entity graph (same shape
                // as the live publish path, which sees them via the
                // attached new-entity navigation collection).
                var plays = await _dataContext.CompetitionPlays
                    .OfType<BaseballCompetitionPlay>()
                    .Include(p => p.Participants)
                    .Where(p => p.CompetitionId == competition.Id)
                    .OrderBy(p => p.EspnId)
                    .ToListAsync(ct);

                _logger.LogInformation(
                    "BaseballReplay: enumerating plays. CompetitionId={CompetitionId}, BaseballPlayCount={BaseballPlayCount}, ContestId={ContestId}, CorrelationId={CorrelationId}",
                    competition.Id, plays.Count, contestId, correlationId);

                if (plays.Count == 0)
                {
                    _logger.LogWarning(
                        "BaseballReplay: no plays to emit. CompetitionPlays returned 0 rows for CompetitionId={CompetitionId}. ContestId={ContestId}, CorrelationId={CorrelationId}",
                        competition.Id, contestId, correlationId);
                }

                var emittedCount = 0;
                foreach (var play in plays)
                {
                    var display = await BaseballPlayCompletedPayloadBuilder
                        .HydrateAsync(_dataContext, play, ct);

                    await _eventBus.Publish(new BaseballPlayCompleted(
                        ContestId: contestId,
                        CompetitionId: competition.Id,
                        PlayId: play.Id,
                        PlayDescription: play.Text,
                        Inning: play.PeriodNumber,
                        HalfInning: play.HalfInning ?? string.Empty,
                        AwayScore: play.AwayScore,
                        HomeScore: play.HomeScore,
                        Balls: play.ResultCountBalls ?? 0,
                        Strikes: play.ResultCountStrikes ?? 0,
                        Outs: play.Outs ?? 0,
                        RunnerOnFirst: false,
                        RunnerOnSecond: false,
                        RunnerOnThird: false,
                        AtBatAthleteSeasonId: play.AtBatAthleteSeasonId,
                        AtBatShortName: display.AtBatShortName,
                        AtBatPositionAbbreviation: display.AtBatPositionAbbreviation,
                        AtBatHeadshotUrl: display.AtBatHeadshotUrl,
                        PitchingAthleteSeasonId: play.PitchingAthleteSeasonId,
                        PitchingShortName: display.PitchingShortName,
                        PitchingPositionAbbreviation: display.PitchingPositionAbbreviation,
                        PitchingHeadshotUrl: display.PitchingHeadshotUrl,
                        Ref: null,
                        Sport: contest.Sport,
                        SeasonYear: contest.SeasonYear,
                        CorrelationId: correlationId,
                        CausationId: CausationId.Producer.EventCompetitionStatusDocumentProcessor
                    ), ct);

                    await _dataContext.SaveChangesAsync(ct);

                    emittedCount++;
                    _logger.LogInformation(
                        "BaseballReplay: published BaseballPlayCompleted. PlayId={PlayId}, EspnId={EspnId}, Inning={Inning}, Score={Away}-{Home}, Count={Balls}-{Strikes}, EmittedCount={EmittedCount}/{TotalPlays}, ContestId={ContestId}, CorrelationId={CorrelationId}",
                        play.Id, play.EspnId, play.PeriodNumber, play.AwayScore, play.HomeScore, play.ResultCountBalls ?? 0, play.ResultCountStrikes ?? 0, emittedCount, plays.Count, contestId, correlationId);

                    await Task.Delay(1000, ct);
                }

                _logger.LogInformation(
                    "BaseballReplay: completed. ContestId={ContestId}, EmittedPlays={EmittedPlays}, CorrelationId={CorrelationId}",
                    contestId, emittedCount, correlationId);
            }
            finally
            {
                // Always restore FinalizedUtc, even if an exception
                // occurred or the incoming token was canceled — the
                // restore must persist or the contest gets stuck in a
                // mid-replay state. Use CancellationToken.None so a
                // tripped ct can't skip the write.
                contest.FinalizedUtc = finalizedPrev;
                await _dataContext.SaveChangesAsync(CancellationToken.None);
            }
        }
    }
}
