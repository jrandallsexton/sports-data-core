using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Plays.Consumers
{
    /// <summary>
    /// Instant-analysis fan-out. Forwards Producer's
    /// <see cref="PlayAnalysisGenerated"/> to SignalR clients as
    /// "PlayAnalysis". Clients attach the facts to the play with the same
    /// PlayId; the event can trail its FootballPlayCompleted by a hop. Broadcast
    /// to all, like every other live event (no per-contest groups yet).
    /// See docs/features/instant-analysis/.
    /// </summary>
    public class PlayAnalysisGeneratedConsumer : IConsumer<PlayAnalysisGenerated>
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<PlayAnalysisGeneratedConsumer> _logger;

        public PlayAnalysisGeneratedConsumer(
            IHubContext<NotificationHub> hubContext,
            ILogger<PlayAnalysisGeneratedConsumer> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PlayAnalysisGenerated> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All
                .SendAsync("PlayAnalysis", msg, context.CancellationToken);

            _logger.LogInformation(
                "PlayAnalysisGenerated consume: sent to SignalR. ContestId={ContestId}, PlayId={PlayId}, Items={ItemCount}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.PlayId, msg.Items.Count, msg.CorrelationId, context.MessageId);
        }
    }
}
