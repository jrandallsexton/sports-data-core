using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Handles ContestScoreChanged events and broadcasts them to connected SignalR clients.
    /// </summary>
    public class ContestScoreChangedHandler : IConsumer<ContestScoreChanged>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public ContestScoreChangedHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<ContestScoreChanged> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All // ? simple, global broadcast for now
                .SendAsync("ContestScoreChanged", msg);
        }
    }
}
