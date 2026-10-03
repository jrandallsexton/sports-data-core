using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Commands.RefreshWeekMatchups;

/// <summary>
/// Re-runs the matchup scheduler over an already-generated week so the record
/// snapshots on PickemGroupMatchup are rewritten from canonical data.
/// </summary>
/// <remarks>
/// Supply <see cref="SeasonWeekId"/> when you have it — it is the precise week
/// identity. Week NUMBERS are ambiguous across both phase and sport (2026 week
/// 2 exists for NCAA football, NFL preseason and NFL regular season at once),
/// so the year/week convenience form reports the candidates rather than
/// guessing. <see cref="Sport"/> narrows that form and is ignored when
/// SeasonWeekId is supplied.
/// </remarks>
public class RefreshWeekMatchupsCommand
{
    public Guid? SeasonWeekId { get; init; }

    public int? SeasonYear { get; init; }

    public int? SeasonWeek { get; init; }

    public Sport? Sport { get; init; }
}
