using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    public interface IContestReplayService
    {
        Task ReplayContest(
            Guid contestId,
            Guid correlationId,
            CancellationToken ct = default);
    }

    public class ContestReplayService : IContestReplayService
    {
        private readonly ILogger<ContestReplayService> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IEventBus _eventBus;

        public ContestReplayService(
            ILogger<ContestReplayService> logger,
            TeamSportDataContext dataContext,
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

            // remove finalization data from the contest
            var contest = await _dataContext.Contests
                .FirstOrDefaultAsync(c => c.Id == contestId, ct);

            if (contest is null)
            {
                _logger.LogError("Contest not found");
                return;
            }

            var competition = await _dataContext.Competitions
                .Include(x => x.Plays)
                .FirstOrDefaultAsync(c => c.ContestId == contestId, ct);

            if (competition is null)
            {
                _logger.LogError("Contest not found");
                return;
            }

            contest.FinalizedUtc = null;
            await _dataContext.SaveChangesAsync(ct);

            await _eventBus.Publish(new ContestStatusChanged(
                contestId,
                ContestStatus.InProgress.ToString(),
                "0","15:00", 0, 0, contest.AwayTeamFranchiseSeasonId, false,
                correlationId,
                CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ), ct);

            var plays = competition.Plays.ToList().OrderBy(x => int.Parse(x.SequenceNumber));

            foreach (var play in plays)
            {
                await _eventBus.Publish(new ContestStatusChanged(
                    contestId,
                    ContestStatus.InProgress.ToString(),
                    $"Q{play.PeriodNumber}",
                    play.ClockDisplayValue ?? "UNK",
                    play.AwayScore,
                    play.HomeScore,
                    play.StartFranchiseSeasonId,
                    play.ScoringPlay,
                    correlationId,
                    CausationId.Producer.EventCompetitionStatusDocumentProcessor
                ), ct);

                await Task.Delay(1000, ct);
            }
        }
    }
}
