using System;

namespace SportsData.Core.Dtos.Canonical;

/// <summary>
/// One instant-analysis fact for a play, ready to display
/// (docs/features/instant-analysis/). Same shape as the SignalR
/// <c>PlayAnalysis</c> message's items, so a card filled from the matchup
/// payload and one filled from SignalR render the same way.
/// </summary>
public class PlayAnalysisItemDto
{
    public Guid Id { get; set; }

    public string FactType { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
