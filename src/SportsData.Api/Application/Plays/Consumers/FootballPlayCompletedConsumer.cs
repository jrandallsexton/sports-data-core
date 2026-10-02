using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests.Football;

namespace SportsData.Api.Application.Plays.Consumers
{
    /// <summary>
    /// Football per-play fan-out. Forwards Producer's
    /// <see cref="FootballPlayCompleted"/> to SignalR clients — drives
    /// both the play-by-play feed and the live game scoreboard
    /// (period, clock, score, possession, scoring flash) from a single
    /// merged event.
    /// </summary>
    public class FootballPlayCompletedConsumer : IConsumer<FootballPlayCompleted>
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<FootballPlayCompletedConsumer> _logger;

        public FootballPlayCompletedConsumer(
            IHubContext<NotificationHub> hubContext,
            ILogger<FootballPlayCompletedConsumer> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<FootballPlayCompleted> context)
        {
            var msg = context.Message;

            _logger.LogInformation(
                "FootballPlayCompleted consume: received. ContestId={ContestId}, PlayId={PlayId}, Period={Period}, Clock={Clock}, CausationId={CausationId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.PlayId, msg.Period, msg.Clock, msg.CausationId, msg.CorrelationId, context.MessageId);

            await _hubContext.Clients
                .All
                .SendAsync("FootballPlayCompleted", msg, context.CancellationToken);

            _logger.LogInformation(
                "FootballPlayCompleted consume: SignalR Clients.All.SendAsync completed. ContestId={ContestId}, PlayId={PlayId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.PlayId, msg.CorrelationId, context.MessageId);
        }
    }
}
