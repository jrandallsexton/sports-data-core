using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests.Commands
{
    public class FinalizeContestsBySeasonYearCommand
    {
        public Sport Sport { get; init; }

        public int SeasonYear { get; init; }

        public Guid CorrelationId { get; init; } = Guid.NewGuid();
    }

    public interface IFinalizeContestsBySeasonYearHandler
    {
        Task<Guid> ExecuteAsync(FinalizeContestsBySeasonYearCommand command);
    }

    public class FinalizeContestsBySeasonYearHandler : IFinalizeContestsBySeasonYearHandler
    {
        private readonly ILogger<FinalizeContestsBySeasonYearHandler> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public FinalizeContestsBySeasonYearHandler(
            ILogger<FinalizeContestsBySeasonYearHandler> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task<Guid> ExecuteAsync(FinalizeContestsBySeasonYearCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["Sport"] = command.Sport,
                       ["SeasonYear"] = command.SeasonYear,
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                return await ExecuteInternalAsync(command);
            }
        }

        private async Task<Guid> ExecuteInternalAsync(FinalizeContestsBySeasonYearCommand command)
        {
            var correlationId = Guid.NewGuid();

            _logger.LogInformation("Finalizing contests");

            var contests = await _dataContext.Contests
                .AsNoTracking()
                .Where(c => c.Sport == command.Sport &&
                            c.SeasonYear == command.SeasonYear &&
                            c.FinalizedUtc == null)
                .ToListAsync();

            foreach (var contest in contests)
            {
                var cmd = new EnrichContestCommand(contest.Id, command.CorrelationId);
                _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(cmd));
            }

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Finalization queueing complete");

            return correlationId;
        }
    }
}
