using MassTransit;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Eventing.Events.Previews;

namespace SportsData.Api.Application.Previews;

public class PreviewGeneratedHandler : IConsumer<PreviewGenerated>
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly AppDataContext _dataContext;
    private readonly ILeagueWeekMatchupsCache _matchupsCache;
    private readonly ILogger<PreviewGeneratedHandler> _logger;

    public PreviewGeneratedHandler(
        IHubContext<NotificationHub> hubContext,
        AppDataContext dataContext,
        ILeagueWeekMatchupsCache matchupsCache,
        ILogger<PreviewGeneratedHandler> logger)
    {
        _hubContext = hubContext;
        _dataContext = dataContext;
        _matchupsCache = matchupsCache;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PreviewGenerated> context)
    {
        var msg = context.Message;

        // The league-week payload carries IsPreviewAvailable per matchup, so every
        // league-week this contest sits in is now stale. Evict BEFORE the broadcast:
        // the client refetches on this event, and a refetch that lands on the old
        // entry shows the card without its preview for the rest of the TTL.
        await EvictLeagueWeeksContainingAsync(msg.ContestId, context.CancellationToken);

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

    private async Task EvictLeagueWeeksContainingAsync(Guid contestId, CancellationToken ct)
    {
        try
        {
            var leagueWeeks = await _dataContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(m => m.ContestId == contestId)
                .Select(m => new { m.GroupId, m.SeasonWeek })
                .Distinct()
                .ToListAsync(ct);

            foreach (var lw in leagueWeeks)
            {
                await _matchupsCache.RemoveAsync(lw.GroupId, lw.SeasonWeek);
            }
        }
        catch (Exception ex)
        {
            // The preview is already persisted and the broadcast must still go out;
            // a failed eviction only means the card catches up when the entry expires.
            _logger.LogWarning(
                ex,
                "Could not evict league-week caches for contest {ContestId} after preview generation.",
                contestId);
        }
    }
}
