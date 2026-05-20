using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.PickemGroups
{
    public class PickemGroupWeekMatchupsGeneratedHandler : IConsumer<PickemGroupWeekMatchupsGenerated>
    {
        private readonly ILogger<PickemGroupWeekMatchupsGeneratedHandler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IEventBus _eventBus;
        private readonly IMessageDeliveryScope _deliveryScope;

        public PickemGroupWeekMatchupsGeneratedHandler(
            ILogger<PickemGroupWeekMatchupsGeneratedHandler> logger,
            AppDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IEventBus eventBus,
            IMessageDeliveryScope deliveryScope)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _eventBus = eventBus;
            _deliveryScope = deliveryScope;
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
                await ConsumeInternal(context.Message, context.CancellationToken);
            }
        }

        private async Task ConsumeInternal(PickemGroupWeekMatchupsGenerated @event, CancellationToken ct)
        {
            // Implement logic to generate AI predictions for matchups in a pick'em group week (if not present)
            var groupWeekMatchups = await _dataContext.PickemGroupMatchups
                .Where(x => x.GroupId == @event.GroupId && x.SeasonYear == @event.SeasonYear && x.SeasonWeek == @event.WeekNumber)
                .ToListAsync(ct);

            if (!groupWeekMatchups.Any())
            {
                _logger.LogWarning("No matchups found. Will retry");
                throw new Exception("No matchups found for group week");
            }

            var groupWeekMatchupsContestIds = groupWeekMatchups.Select(x => x.ContestId).ToList();

            // Request a refresh for every contest in the week. Contests may have
            // been added to the group before their ESPN metadata (probable
            // pitchers, opening spread, broadcasts) was fully populated; this
            // fans out a per-contest refresh so the UI fills in quickly.
            // Direct delivery — this consumer does no DbContext writes, so the
            // outbox isn't involved.
            using (_deliveryScope.Use(DeliveryMode.Direct))
            {
                foreach (var contestId in groupWeekMatchupsContestIds)
                {
                    await _eventBus.Publish(new ContestRefreshRequested(
                        contestId,
                        null,
                        @event.Sport,
                        @event.SeasonYear,
                        @event.CorrelationId,
                        Guid.NewGuid()),
                        ct);
                }
            }

            var existingPreviews = await _dataContext.MatchupPreviews
                .Where(p => groupWeekMatchupsContestIds.Contains(p.ContestId))
                .ToListAsync(ct);

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
