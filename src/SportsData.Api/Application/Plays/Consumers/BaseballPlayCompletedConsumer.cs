using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests.Baseball;

namespace SportsData.Api.Application.Plays.Consumers
{
    /// <summary>
    /// Baseball per-play fan-out. Forwards Producer's
    /// <see cref="BaseballPlayCompleted"/> to SignalR clients — drives
    /// both the play-by-play feed and the diamond / scoreboard
    /// (inning, count, outs, base state, current at-bat) from a single
    /// merged event.
    /// </summary>
    public class BaseballPlayCompletedConsumer : IConsumer<BaseballPlayCompleted>
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<BaseballPlayCompletedConsumer> _logger;

        public BaseballPlayCompletedConsumer(
            IHubContext<NotificationHub> hubContext,
            ILogger<BaseballPlayCompletedConsumer> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<BaseballPlayCompleted> context)
        {
            var msg = context.Message;

            _logger.LogInformation(
                "BaseballPlayCompleted consume: received. ContestId={ContestId}, PlayId={PlayId}, Inning={Inning} {HalfInning}, CausationId={CausationId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.PlayId, msg.Inning, msg.HalfInning, msg.CausationId, msg.CorrelationId, context.MessageId);

            await _hubContext.Clients
                .All
                .SendAsync("BaseballPlayCompleted", msg, context.CancellationToken);

            _logger.LogInformation(
                "BaseballPlayCompleted consume: SignalR Clients.All.SendAsync completed. ContestId={ContestId}, PlayId={PlayId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.PlayId, msg.CorrelationId, context.MessageId);
        }
    }
}
