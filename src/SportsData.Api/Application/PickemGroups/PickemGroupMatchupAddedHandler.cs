using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.PickemGroups
{
    public class PickemGroupMatchupAddedHandler : IConsumer<PickemGroupMatchupAdded>
    {
        private readonly ILogger<PickemGroupMatchupAddedHandler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public PickemGroupMatchupAddedHandler(
            ILogger<PickemGroupMatchupAddedHandler> logger,
            AppDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Consume(ConsumeContext<PickemGroupMatchupAdded> context)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = context.Message.CorrelationId,
                       ["GroupId"] = context.Message.GroupId,
                       ["ContestId"] = context.Message.ContestId
                   }))
            {
                _logger.LogInformation("Processing AI Preview for added matchup. {@Message}", context.Message);
                await ConsumeInternal(context.Message);
            }
        }

        private async Task ConsumeInternal(PickemGroupMatchupAdded @event)
        {
            // Check if preview already exists for this contest
            var previewExists = await _dataContext.MatchupPreviews
                .AnyAsync(p => p.ContestId == @event.ContestId && p.RejectedUtc == null);

            if (previewExists)
            {
                _logger.LogInformation(
                    "Preview already exists for ContestId={ContestId}. Skipping generation.",
                    @event.ContestId);
                return;
            }

            // Enqueue preview generation job
            var cmd = new GenerateMatchupPreviewsCommand
            {
                ContestId = @event.ContestId
            };

            _backgroundJobProvider.Enqueue<MatchupPreviewProcessor>(p => p.Process(cmd));

            _logger.LogInformation(
                "Enqueued AI preview generation for ContestId={ContestId}",
                @event.ContestId);
        }
    }
}
