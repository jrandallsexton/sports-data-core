using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    public class ContestReplayService
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

            contest.FinalizedUtc = null;
            await _dataContext.SaveChangesAsync(ct);

            await _eventBus.Publish(new ContestStatusChanged(
                contestId,
                ContestStatus.InProgress.ToString(),
                correlationId,
                CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ), ct);

            var competition = await _dataContext.Competitions
                .Include(x => x.Plays)
                .FirstOrDefaultAsync(c => c.Id == contestId, ct);
        }
    }
}
