using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Sport-neutral lifecycle fan-out. Forwards Producer's
    /// <see cref="ContestStatusChanged"/> to all SignalR clients under the
    /// same event name. Per-play updates are handled separately by
    /// <see cref="FootballPlayCompletedHandler"/> /
    /// <see cref="BaseballPlayCompletedHandler"/>, which carry both the
    /// play description and the sport-specific scoreboard tick.
    /// </summary>
    public class ContestStatusChangedHandler : IConsumer<ContestStatusChanged>
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<ContestStatusChangedHandler> _logger;

        public ContestStatusChangedHandler(
            IHubContext<NotificationHub> hubContext,
            ILogger<ContestStatusChangedHandler> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ContestStatusChanged> context)
        {
            var msg = context.Message;

            _logger.LogInformation(
                "ContestStatusChanged consume: received. ContestId={ContestId}, Status={Status}, StatusDescription={StatusDescription}, Sport={Sport}, CausationId={CausationId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.Status, msg.StatusDescription, msg.Sport, msg.CausationId, msg.CorrelationId, context.MessageId);

            await _hubContext.Clients
                .All // ← simple, global broadcast for now
                .SendAsync("ContestStatusChanged", msg, context.CancellationToken);

            _logger.LogInformation(
                "ContestStatusChanged consume: SignalR Clients.All.SendAsync completed. ContestId={ContestId}, Status={Status}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.Status, msg.CorrelationId, context.MessageId);
        }
    }
}
