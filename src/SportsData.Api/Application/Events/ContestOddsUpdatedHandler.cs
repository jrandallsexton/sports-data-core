using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Events
{
    public class ContestOddsUpdatedHandler : IConsumer<ContestOddsUpdated>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public ContestOddsUpdatedHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<ContestOddsUpdated> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All // ← simple, global broadcast for now
                .SendAsync("ContestOddsUpdated", new
                {
                    msg.ContestId,
                    msg.Message,
                    msg.CorrelationId,
                    msg.CausationId
                });
        }
    }
}
