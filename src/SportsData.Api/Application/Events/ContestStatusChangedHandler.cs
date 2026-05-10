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

        public ContestStatusChangedHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<ContestStatusChanged> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All // ← simple, global broadcast for now
                .SendAsync("ContestStatusChanged", msg, context.CancellationToken);
        }
    }
}
