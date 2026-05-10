using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests.Football;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Football per-play fan-out. Forwards Producer's
    /// <see cref="FootballPlayCompleted"/> to SignalR clients — drives
    /// both the play-by-play feed and the live game scoreboard
    /// (period, clock, score, possession, scoring flash) from a single
    /// merged event.
    /// </summary>
    public class FootballPlayCompletedHandler : IConsumer<FootballPlayCompleted>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public FootballPlayCompletedHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<FootballPlayCompleted> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All
                .SendAsync("FootballPlayCompleted", msg, context.CancellationToken);
        }
    }
}
