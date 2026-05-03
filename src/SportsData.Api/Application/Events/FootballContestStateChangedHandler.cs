using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests.Football;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Football scoreboard fan-out. Forwards Producer's per-play
    /// <see cref="FootballContestStateChanged"/> to SignalR clients —
    /// drives the live game UI (period, clock, score, possession, scoring
    /// flash).
    /// </summary>
    public class FootballContestStateChangedHandler : IConsumer<FootballContestStateChanged>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public FootballContestStateChangedHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<FootballContestStateChanged> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All
                .SendAsync("FootballContestStateChanged", msg);
        }
    }
}
