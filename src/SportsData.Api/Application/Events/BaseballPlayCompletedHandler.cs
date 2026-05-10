using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests.Baseball;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Baseball per-play fan-out. Forwards Producer's
    /// <see cref="BaseballPlayCompleted"/> to SignalR clients — drives
    /// both the play-by-play feed and the diamond / scoreboard
    /// (inning, count, outs, base state, current at-bat) from a single
    /// merged event.
    /// </summary>
    public class BaseballPlayCompletedHandler : IConsumer<BaseballPlayCompleted>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public BaseballPlayCompletedHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<BaseballPlayCompleted> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All
                .SendAsync("BaseballPlayCompleted", msg, context.CancellationToken);
        }
    }
}
