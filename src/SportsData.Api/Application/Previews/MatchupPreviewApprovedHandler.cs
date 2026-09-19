using MassTransit;

using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Core.Eventing.Events.Previews;

namespace SportsData.Api.Application.Previews;

/// <summary>
/// StatBot's pick is re-derived from the preview that was APPROVED — by id,
/// not "the latest non-rejected one", because approving an older preview while
/// a newer one exists must not persist the newer prediction. It changes
/// only when the approved preview names a different winner than the one
/// already picked, and never after kickoff. Approval also flips
/// IsPreviewReviewed in the cached league-week payload, which PreviewService
/// evicts; this evicts again after the pick write for the same reason.
/// </summary>
public class MatchupPreviewApprovedHandler : IConsumer<MatchupPreviewApproved>
{
    private readonly IStatBotPickWriter _statBotPickWriter;
    private readonly ILeagueWeekMatchupsCacheInvalidator _cacheInvalidator;

    public MatchupPreviewApprovedHandler(
        IStatBotPickWriter statBotPickWriter,
        ILeagueWeekMatchupsCacheInvalidator cacheInvalidator)
    {
        _statBotPickWriter = statBotPickWriter;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task Consume(ConsumeContext<MatchupPreviewApproved> context)
    {
        var written = await _statBotPickWriter.UpsertForContestAsync(
            context.Message.ContestId,
            context.Message.MatchupPreviewId,
            context.CancellationToken);
        if (written > 0)
            await _cacheInvalidator.EvictForContestAsync(context.Message.ContestId, context.CancellationToken);
    }
}
