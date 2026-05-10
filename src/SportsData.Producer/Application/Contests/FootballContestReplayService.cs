using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Contests.Football;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Contests
{
    public interface IFootballContestReplayService
    {
        Task ReplayContest(
            Guid contestId,
            Guid correlationId,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Replays a completed football contest by emitting the lifecycle event
    /// once and then a per-play <see cref="FootballPlayCompleted"/> for
    /// each stored play. Used by the admin replay endpoints to drive the
    /// live UI through a known game without needing a real ESPN feed.
    /// Baseball has its own equivalent in <see cref="BaseballContestReplayService"/>.
    /// </summary>
    public class FootballContestReplayService : IFootballContestReplayService
    {
        private readonly ILogger<FootballContestReplayService> _logger;
        private readonly FootballDataContext _dataContext;
        private readonly IEventBus _eventBus;

        public FootballContestReplayService(
            ILogger<FootballContestReplayService> logger,
            FootballDataContext dataContext,
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
                "FootballReplay: starting. ContestId={ContestId}, CorrelationId={CorrelationId}",
                contestId, correlationId);

            // remove finalization data from the contest
            var contest = await _dataContext.Contests
                .FirstOrDefaultAsync(c => c.Id == contestId, ct);

            if (contest is null)
            {
                _logger.LogError(
                    "FootballReplay: contest not found. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId, correlationId);
                return;
            }

            _logger.LogInformation(
                "FootballReplay: contest loaded. ContestId={ContestId}, Sport={Sport}, SeasonYear={SeasonYear}, FinalizedUtc={FinalizedUtc}, CorrelationId={CorrelationId}",
                contestId, contest.Sport, contest.SeasonYear, contest.FinalizedUtc, correlationId);

            var competition = await _dataContext.Competitions
                .FirstOrDefaultAsync(c => c.ContestId == contestId, ct);

            if (competition is null)
            {
                _logger.LogError(
                    "FootballReplay: competition not found. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId, correlationId);
                return;
            }

            _logger.LogInformation(
                "FootballReplay: competition loaded. CompetitionId={CompetitionId}, ContestId={ContestId}, CorrelationId={CorrelationId}",
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
                    "FootballReplay: published ContestStatusChanged=InProgress. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId, correlationId);

                // Query CompetitionPlays directly off the DbSet rather
                // than the navigation collection — the navigation came
                // back empty in practice (no Include + no lazy-loading
                // proxies) even when stored plays exist. Same fix as
                // BaseballContestReplayService.
                //
                // Sort is done in-memory after materialization: EF can't
                // translate int.TryParse, and a hard int.Parse aborts
                // the entire replay if ESPN ever emits a non-numeric
                // SequenceNumber. Non-numeric values sort last via the
                // int.MaxValue sentinel so a single bad row doesn't
                // poison the rest of the stream.
                var fetched = await _dataContext.CompetitionPlays
                    .OfType<FootballCompetitionPlay>()
                    .Where(p => p.CompetitionId == competition.Id)
                    .ToListAsync(ct);

                var plays = fetched
                    .OrderBy(p => int.TryParse(p.SequenceNumber, out var n) ? n : int.MaxValue)
                    .ThenBy(p => p.SequenceNumber, StringComparer.Ordinal)
                    .ToList();

                _logger.LogInformation(
                    "FootballReplay: enumerating plays. CompetitionId={CompetitionId}, FootballPlayCount={FootballPlayCount}, ContestId={ContestId}, CorrelationId={CorrelationId}",
                    competition.Id, plays.Count, contestId, correlationId);

                if (plays.Count == 0)
                {
                    _logger.LogWarning(
                        "FootballReplay: no plays to emit. CompetitionPlays returned 0 rows for CompetitionId={CompetitionId}. ContestId={ContestId}, CorrelationId={CorrelationId}",
                        competition.Id, contestId, correlationId);
                }

                var emittedCount = 0;
                foreach (var play in plays)
                {
                    // Merged per-play event — play description + scoreboard
                    // tick in one message so the UI doesn't reassemble.
                    await _eventBus.Publish(new FootballPlayCompleted(
                        ContestId: contestId,
                        CompetitionId: competition.Id,
                        PlayId: play.Id,
                        PlayDescription: play.Text,
                        Period: $"Q{play.PeriodNumber}",
                        Clock: play.ClockDisplayValue ?? "UNK",
                        AwayScore: play.AwayScore,
                        HomeScore: play.HomeScore,
                        PossessionFranchiseSeasonId: play.StartFranchiseSeasonId,
                        IsScoringPlay: play.ScoringPlay,
                        BallOnYardLine: play.EndYardLine ?? play.StartYardLine,
                        Ref: null,
                        Sport: contest.Sport,
                        SeasonYear: contest.SeasonYear,
                        CorrelationId: correlationId,
                        CausationId: CausationId.Producer.EventCompetitionStatusDocumentProcessor
                    ), ct);

                    await _dataContext.SaveChangesAsync(ct);

                    emittedCount++;
                    _logger.LogInformation(
                        "FootballReplay: published FootballPlayCompleted. PlayId={PlayId}, SequenceNumber={SequenceNumber}, Period=Q{Period}, Clock={Clock}, Score={Away}-{Home}, Scoring={Scoring}, EmittedCount={EmittedCount}/{TotalPlays}, ContestId={ContestId}, CorrelationId={CorrelationId}",
                        play.Id, play.SequenceNumber, play.PeriodNumber, play.ClockDisplayValue ?? "UNK", play.AwayScore, play.HomeScore, play.ScoringPlay, emittedCount, plays.Count, contestId, correlationId);

                    await Task.Delay(1000, ct);
                }

                _logger.LogInformation(
                    "FootballReplay: completed. ContestId={ContestId}, EmittedPlays={EmittedPlays}, CorrelationId={CorrelationId}",
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
