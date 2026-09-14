using MassTransit;

using Microsoft.AspNetCore.SignalR;

using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Previews;

namespace SportsData.Api.Application.Previews;

public class PreviewGeneratedHandler : IConsumer<PreviewGenerated>
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILeagueWeekMatchupsCacheInvalidator _cacheInvalidator;

    public PreviewGeneratedHandler(
        IHubContext<NotificationHub> hubContext,
        ILeagueWeekMatchupsCacheInvalidator cacheInvalidator)
    {
        _hubContext = hubContext;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task Consume(ConsumeContext<PreviewGenerated> context)
    {
        var msg = context.Message;

        // The league-week payload carries IsPreviewAvailable per matchup, so every
        // league-week this contest sits in is now stale. Evict BEFORE the broadcast:
        // the client refetches on this event, and a refetch that lands on the old
        // entry shows the card without its preview for the rest of the TTL.
        await _cacheInvalidator.EvictForContestAsync(msg.ContestId, context.CancellationToken);

        await _hubContext.Clients
            .All // ← simple, global broadcast for now
            .SendAsync(nameof(PreviewGenerated), new
            {
                msg.ContestId,
                msg.Message,
                msg.CorrelationId,
                msg.CausationId
            });
    }
}
