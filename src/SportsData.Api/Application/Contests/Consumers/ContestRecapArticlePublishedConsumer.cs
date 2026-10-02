using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Contests.Consumers
{
    public class ContestRecapArticlePublishedConsumer : IConsumer<ContestRecapArticlePublished>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public ContestRecapArticlePublishedConsumer(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<ContestRecapArticlePublished> context)
        {
            var msg = context.Message;

            await _hubContext.Clients
                .All // ← simple, global broadcast for now
                .SendAsync("ContestRecapArticlePublished", new
                {
                    msg.ContestId,
                    msg.Title,
                    msg.ArticleId
                });
        }
    }
}
