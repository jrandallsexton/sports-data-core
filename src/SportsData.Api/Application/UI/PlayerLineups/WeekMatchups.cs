using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.UI.PlayerLineups;

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
