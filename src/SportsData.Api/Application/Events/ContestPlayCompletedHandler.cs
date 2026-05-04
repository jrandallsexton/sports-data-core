using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Sport-neutral per-play log fan-out. Forwards Producer's
    /// <see cref="ContestPlayCompleted"/> to SignalR clients so the live
    /// game UI can render a play-by-play feed alongside the sport-specific
    /// scoreboard tick (Football/BaseballContestStateChanged).
    /// </summary>
    public class ContestPlayCompletedHandler : IConsumer<ContestPlayCompleted>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public ContestPlayCompletedHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<ContestPlayCompleted> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All
                .SendAsync("ContestPlayCompleted", msg, context.CancellationToken);
        }
    }
}
