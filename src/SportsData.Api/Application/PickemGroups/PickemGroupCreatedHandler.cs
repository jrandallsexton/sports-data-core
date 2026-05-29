using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.PickemGroups
{
    /// <summary>
    /// Creation-time fan-out point for post-PickemGroupCreated work. Today it
    /// kicks off matchup bootstrap via <see cref="IBootstrapLeagueMatchups"/>;
    /// future side-effects (invitations from a prior league, notifications,
    /// etc.) get layered in here without disturbing the matchup pipeline.
    ///
    /// <para>
    /// The handler itself does **no** SeasonWeek lookup or
    /// <c>PickemGroupWeek</c> creation — that responsibility moved into the
    /// Hangfire-side <c>BootstrapLeagueMatchupsProcessor</c>, which owns the
    /// "decide windowed vs full-season, resolve SeasonWeek(s), fan out
    /// per-week jobs" dispatch. Splitting the work this way means the
    /// MassTransit consumer stays fast (single DB existence check + enqueue)
    /// and the heavy lifting runs under Hangfire's retry/persistence model.
    /// See <c>docs/league-creation-matrix.md</c> for the design space.
    /// </para>
    /// </summary>
    public class PickemGroupCreatedHandler : IConsumer<PickemGroupCreated>
    {
        private readonly ILogger<PickemGroupCreatedHandler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public PickemGroupCreatedHandler(
            ILogger<PickemGroupCreatedHandler> logger,
            AppDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Consume(ConsumeContext<PickemGroupCreated> context)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = context.Message.CorrelationId
                   }))
            {
                _logger.LogInformation(
                    "New pickem group created event received: {@Message}",
                    context.Message);

                await DispatchAsync(context.Message);
            }
        }

        private async Task DispatchAsync(PickemGroupCreated @event)
        {
            var groupExists = await _dataContext.PickemGroups
                .AsNoTracking()
                .AnyAsync(x => x.Id == @event.GroupId);

            if (!groupExists)
            {
                // Permanent failure: a missing group doesn't fix itself on
                // retry. Log and return rather than throw — throwing would
                // land the message in the DLQ for a state that will never
                // become valid.
                _logger.LogError(
                    "PickemGroupCreated received for unknown group {GroupId}; skipping bootstrap.",
                    @event.GroupId);
                return;
            }

            var cmd = new BootstrapLeagueMatchupsCommand(
                @event.GroupId,
                @event.CorrelationId);
            _backgroundJobProvider.Enqueue<IBootstrapLeagueMatchups>(p => p.Process(cmd));
        }
    }
}
