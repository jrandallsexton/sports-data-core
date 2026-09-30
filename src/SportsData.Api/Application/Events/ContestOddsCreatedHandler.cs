using MassTransit;

using SportsData.Api.Application.Processors;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// A contest's first odds arrived. Same rule as
    /// <see cref="ContestOddsUpdatedHandler"/>: when the event carries the
    /// DISPLAYED row (<see cref="ContestOddsCreated.DisplayedOdds"/>), enqueue
    /// <see cref="IApplyMatchupOdds"/> for the contest's PickemGroupMatchups.
    /// Covers odds that first appear after a matchup already exists, which the
    /// matchup schedule processor (reading odds at creation) cannot see.
    ///
    /// Requires the per-sport contest-odds-created shovels to the API broker
    /// (sports-data-config); without them this consumer receives nothing.
    ///
    /// Thin Hangfire-spawn shim per the ingest-consumer convention: no inline
    /// DB work.
    /// </summary>
    public class ContestOddsCreatedHandler : IConsumer<ContestOddsCreated>
    {
        private readonly ILogger<ContestOddsCreatedHandler> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestOddsCreatedHandler(
            ILogger<ContestOddsCreatedHandler> logger,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public Task Consume(ConsumeContext<ContestOddsCreated> context)
        {
            var msg = context.Message;

            if (msg.DisplayedOdds is null)
            {
                _logger.LogDebug(
                    "ContestOddsCreated without displayed odds (not the displayed book); ignored. ContestId={ContestId}",
                    msg.ContestId);
                return Task.CompletedTask;
            }

            var cmd = new ApplyMatchupOddsCommand(msg.ContestId, msg.Sport, msg.DisplayedOdds, msg.CorrelationId);
            _backgroundJobProvider.Enqueue<IApplyMatchupOdds>(p => p.Process(cmd));

            _logger.LogInformation(
                "ContestOddsCreated: displayed odds applied via job. ContestId={ContestId}, ProviderId={ProviderId}, CorrelationId={CorrelationId}",
                msg.ContestId, msg.DisplayedOdds.ProviderId, msg.CorrelationId);

            return Task.CompletedTask;
        }
    }
}
