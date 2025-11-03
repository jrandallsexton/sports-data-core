using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Events
{
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
                .SendAsync("ContestStatusChanged", msg);
        }
    }
}
