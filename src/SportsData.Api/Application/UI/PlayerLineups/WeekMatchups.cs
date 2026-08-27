using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.PlayerLineups;

/// <summary>
/// Resolves the week's matchups for a LEAGUE week. Prefers the league's
/// own PickemGroupWeek row: its SeasonWeekId is the precise (phase-
/// qualified) identity, so a preseason-only league anchors to preseason
/// games — a bare (year, number) lookup is regular-scoped and would
/// anchor to the wrong phase. Falls back to the number query for weeks
/// the league hasn't materialized (Player Pick'em leagues may play
/// weeks with no team-pick slate).
/// </summary>
internal static class LeagueWeekMatchupResolver
{
    internal static async Task<Result<List<Matchup>>> ResolveAsync(
        AppDataContext dataContext,
        IProvideContests contestClient,
        Guid leagueId,
        int seasonYear,
        int seasonWeek,
        CancellationToken cancellationToken)
    {
        var seasonWeekId = await dataContext.PickemGroupWeeks
            .AsNoTracking()
            .Where(w => w.GroupId == leagueId &&
                        w.SeasonYear == seasonYear &&
                        w.SeasonWeek == seasonWeek)
            .OrderBy(w => w.SeasonPhaseTypeCode)
            .Select(w => (Guid?)w.SeasonWeekId)
            .FirstOrDefaultAsync(cancellationToken);

        return seasonWeekId is not null
            ? await contestClient.GetMatchupsBySeasonWeekId(seasonWeekId.Value, cancellationToken)
            : await contestClient.GetMatchupsForSeasonWeek(seasonYear, seasonWeek, cancellationToken);
    }
}

/// <summary>
/// Team-slug → (ContestId, StartUtc) lookup for one season-week, built
/// from ONE ContestClient.GetMatchupsForSeasonWeek call. This is the
/// server-side authority for slot locking and contest anchoring — client
/// -provided contest fields are never trusted. A team absent from the map
/// is on a bye.
/// </summary>
public sealed class WeekMatchupMap
{
    private readonly Dictionary<string, (Guid ContestId, DateTime StartUtc)> _bySlug;

    public WeekMatchupMap(IEnumerable<Matchup> matchups)
    {
        _bySlug = new Dictionary<string, (Guid, DateTime)>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in matchups)
        {
            // StartDateUtc arrives over the wire with DateTimeKind.Unspecified
            // (JSON round-trip strips the kind), and Npgsql refuses to write
            // an Unspecified DateTime to a timestamptz column — the write
            // path stores this value on PlayerLineupSlot.ContestStartUtc.
            // The value is semantically UTC; stamp it so every consumer
            // (persist + IsStartLocked comparisons) agrees.
            var startUtc = DateTime.SpecifyKind(m.StartDateUtc, DateTimeKind.Utc);
            _bySlug[m.HomeSlug] = (m.ContestId, startUtc);
            _bySlug[m.AwaySlug] = (m.ContestId, startUtc);
        }
    }

    public (Guid ContestId, DateTime StartUtc)? Resolve(string teamSlug) =>
        _bySlug.TryGetValue(teamSlug, out var hit) ? hit : null;
}
