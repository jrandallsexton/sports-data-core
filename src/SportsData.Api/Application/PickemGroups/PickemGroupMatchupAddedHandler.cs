using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Api.Application.Previews;
using SportsData.Api.Config;
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
        private readonly ApiConfig _config;

        public PickemGroupMatchupAddedHandler(
            ILogger<PickemGroupMatchupAddedHandler> logger,
            AppDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IOptions<ApiConfig> config)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _config = config.Value;
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
            // Same config kill-switch as PickemGroupWeekMatchupsGeneratedHandler —
            // BOTH preview enqueue paths must honor it or a Local run still
            // burns model tokens on single-matchup additions.
            if (!_config.MatchupPreviewGenerationEnabled)
            {
                _logger.LogInformation(
                    "Matchup preview generation disabled by config. Skipping preview enqueue for contest {ContestId}.",
                    @event.ContestId);
                return;
            }

            // Sport gate — mirrors PickemGroupWeekMatchupsGeneratedHandler;
            // MatchupPreviewProcessor enforces the same policy as the choke point.
            if (!MatchupPreviewPolicy.SupportsSport(@event.Sport))
            {
                _logger.LogInformation(
                    "Preview generation not supported for {Sport}; skipping enqueue for contest {ContestId}.",
                    @event.Sport, @event.ContestId);
                return;
            }

            // Check if preview already exists for this contest
            var previewExists = await _dataContext.MatchupPreviews
                .AnyAsync(p => p.ContestId == @event.ContestId && p.RejectedUtc == null);

            if (previewExists)
            {
                _logger.LogInformation("Preview already exists. Skipping generation.");
                return;
            }

            // Enqueue preview generation job
            var cmd = new GenerateMatchupPreviewsCommand
            {
                ContestId = @event.ContestId,
                Sport = @event.Sport
            };

            _backgroundJobProvider.Enqueue<MatchupPreviewProcessor>(p => p.Process(cmd));

            _logger.LogInformation("Enqueued AI preview generation.");
        }
    }
}
