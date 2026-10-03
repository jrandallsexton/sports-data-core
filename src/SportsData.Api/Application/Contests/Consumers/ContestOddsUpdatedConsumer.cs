using MassTransit;

using SportsData.Api.Application.Matchups.Jobs.ApplyMatchupOdds;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Contests.Consumers
{
    /// <summary>
    /// A contest's odds changed. When the event carries the DISPLAYED row
    /// (<see cref="ContestOddsUpdated.DisplayedOdds"/>: the book the matchup
    /// cards read), enqueue <see cref="IApplyMatchupOdds"/> to write the line and
    /// prices onto the contest's PickemGroupMatchups. Every other book's
    /// update, and events from pods on the prior shape, carry no snapshot and
    /// are ignored.
    ///
    /// Previously this broadcast the event over SignalR to every client, which
    /// no web or mobile code listened to, and never updated a matchup.
    ///
    /// Thin Hangfire-spawn shim per the ingest-consumer convention: no inline
    /// DB work.
    /// </summary>
    public class ContestOddsUpdatedConsumer : IConsumer<ContestOddsUpdated>
    {
        private readonly ILogger<ContestOddsUpdatedConsumer> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestOddsUpdatedConsumer(
            ILogger<ContestOddsUpdatedConsumer> logger,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public Task Consume(ConsumeContext<ContestOddsUpdated> context)
        {
            var msg = context.Message;

            if (msg.DisplayedOdds is null)
            {
                _logger.LogDebug(
                    "ContestOddsUpdated without displayed odds (another book changed); ignored. ContestId={ContestId}, ProviderId={ProviderId}",
                    msg.ContestId, msg.ProviderId);
                return Task.CompletedTask;
            }

            // The event's CreatedUtc (stamped by the Producer) is the odds version.
            var cmd = new ApplyMatchupOddsCommand(msg.ContestId, msg.Sport, msg.DisplayedOdds, msg.CorrelationId, msg.CreatedUtc);
            _backgroundJobProvider.Enqueue<IApplyMatchupOdds>(p => p.Process(cmd));

            _logger.LogInformation(
                "ContestOddsUpdated: displayed odds applied via job. ContestId={ContestId}, ProviderId={ProviderId}, CorrelationId={CorrelationId}",
                msg.ContestId, msg.DisplayedOdds.ProviderId, msg.CorrelationId);

            return Task.CompletedTask;
        }
    }
}
