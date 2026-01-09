using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.PickemGroups
{
    public class PickemGroupWeekMatchupsGeneratedHandler : IConsumer<PickemGroupWeekMatchupsGenerated>
    {
        private readonly ILogger<PickemGroupWeekMatchupsGeneratedHandler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public PickemGroupWeekMatchupsGeneratedHandler(
            ILogger<PickemGroupWeekMatchupsGeneratedHandler> logger,
            AppDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Consume(ConsumeContext<PickemGroupWeekMatchupsGenerated> context)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = context.Message.CorrelationId,
                       ["GroupId"] = context.Message.GroupId,
                       ["SeasonYear"] = context.Message.SeasonYear ?? 0,
                       ["WeekNumber"] = context.Message.WeekNumber
                   }))
            {
                _logger.LogInformation("Processing AI Previews. {@Message}", context.Message);
                await ConsumeInternal(context.Message);
            }
        }

        private async Task ConsumeInternal(PickemGroupWeekMatchupsGenerated @event)
        {
            // Implement logic to generate AI predictions for matchups in a pick'em group week (if not present)
            var groupWeekMatchups = await _dataContext.PickemGroupMatchups
                .Where(x => x.GroupId == @event.GroupId && x.SeasonYear == @event.SeasonYear && x.SeasonWeek == @event.WeekNumber)
                .ToListAsync();

            if (!groupWeekMatchups.Any())
            {
                _logger.LogWarning("No matchups found. Will retry");
                throw new Exception("No matchups found for group week");
            }

            var groupWeekMatchupsContestIds = groupWeekMatchups.Select(x => x.ContestId).ToList();

            var existingPreviews = await _dataContext.MatchupPreviews
                .Where(p => groupWeekMatchupsContestIds.Contains(p.ContestId))
                .ToListAsync();

            var existingPreviewsContestIds = existingPreviews.Select(x => x.ContestId).ToList();

            var contestIdsToGenerate = groupWeekMatchupsContestIds.Except(existingPreviewsContestIds);

            foreach (var contestId in contestIdsToGenerate)
            {
                var cmd = new GenerateMatchupPreviewsCommand()
                {
                    ContestId = contestId
                };

                _backgroundJobProvider.Enqueue<MatchupPreviewProcessor>(p => p.Process(cmd));
            }
        }
    }
}
