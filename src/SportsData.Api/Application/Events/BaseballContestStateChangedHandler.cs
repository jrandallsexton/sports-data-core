using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests.Baseball;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Baseball scoreboard fan-out. Forwards Producer's per-pitch /
    /// per-at-bat <see cref="BaseballContestStateChanged"/> to SignalR
    /// clients. Wired ahead of the MLB live pipeline so the consumer
    /// surface lands in the same PR as the event type.
    /// </summary>
    public class BaseballContestStateChangedHandler : IConsumer<BaseballContestStateChanged>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public BaseballContestStateChangedHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<BaseballContestStateChanged> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All
                .SendAsync("BaseballContestStateChanged", msg, context.CancellationToken);
        }
    }
}
