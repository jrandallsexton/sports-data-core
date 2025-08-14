using System;
using System.Linq;

namespace SportsData.Core.Infrastructure.DataSources.Espn;

public static class EspnUriMapper
{
    public static Uri TeamSeasonToFranchiseRef(Uri teamSeasonRef)
    {
        if (teamSeasonRef == null) throw new ArgumentNullException(nameof(teamSeasonRef));

        var s = teamSeasonRef.ToString();
        var parts = s.Split('/');

        // Expect: ... / seasons / {year} / teams / {teamId}[?qs]
        var seasonsIndex = Array.IndexOf(parts, "seasons");
        var teamsIndex = Array.IndexOf(parts, "teams");

        if (seasonsIndex < 0 || teamsIndex < 0 || teamsIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN TeamSeason ref format: {teamSeasonRef}");

        // Extract teamId (strip query if present on the last segment)
        var teamIdPart = parts[teamsIndex + 1];
        var q = teamIdPart.IndexOf('?');
        var teamId = q >= 0 ? teamIdPart[..q] : teamIdPart;

        // NEW: guard invalid/missing ID
        if (string.IsNullOrWhiteSpace(teamId) || !teamId.All(char.IsDigit))
            throw new InvalidOperationException($"Missing or invalid team id in ref: {teamSeasonRef}");

        // Build base up to *before* "seasons"
        var baseParts = parts.Take(seasonsIndex)
            .Append("franchises")
            .Append(teamId);

        var path = string.Join('/', baseParts);
        var query = teamSeasonRef.Query; // preserves ?lang=en&region=us, etc.

        return new Uri(path + query, UriKind.Absolute);
    }
}
