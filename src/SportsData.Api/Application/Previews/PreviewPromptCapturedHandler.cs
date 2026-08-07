using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Previews;

namespace SportsData.Api.Application.Previews;

public class PreviewPromptCapturedHandler : IConsumer<PreviewPromptCaptured>
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public PreviewPromptCapturedHandler(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<PreviewPromptCaptured> context)
    {
        var msg = context.Message;

        await _hubContext.Clients
            .All // ← same global broadcast as PreviewGenerated
            .SendAsync(nameof(PreviewPromptCaptured), new
            {
                msg.ContestId,
                msg.Message,
                msg.CorrelationId,
                msg.CausationId
            });
    }
}
