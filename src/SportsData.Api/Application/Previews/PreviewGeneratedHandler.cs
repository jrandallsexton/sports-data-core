using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Previews;

namespace SportsData.Api.Application.Previews;

public class PreviewGeneratedHandler : IConsumer<PreviewGenerated>
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public PreviewGeneratedHandler(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<PreviewGenerated> context)
    {
        var msg = context.Message;

        await _hubContext.Clients
            .All // ← simple, global broadcast for now
            .SendAsync("PreviewCompleted", new
            {
                msg.ContestId,
                msg.Message,
                msg.CorrelationId,
                msg.CausationId
            });
    }
}