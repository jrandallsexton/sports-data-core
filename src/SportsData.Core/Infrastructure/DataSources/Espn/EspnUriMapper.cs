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

    public static Uri AthleteSeasonToAthleteRef(Uri athleteSeasonRef)
    {
        if (athleteSeasonRef == null) throw new ArgumentNullException(nameof(athleteSeasonRef));

        var s = athleteSeasonRef.ToString();
        var parts = s.Split('/');

        // Expect: ... / seasons / {year} / athletes / {athleteId}[?qs]
        var seasonsIndex = Array.IndexOf(parts, "seasons");
        var athletesIndex = Array.IndexOf(parts, "athletes");

        if (seasonsIndex < 0 || athletesIndex < 0 || athletesIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN AthleteSeason ref format: {athleteSeasonRef}");

        // Extract athleteId (strip query if present on the last segment)
        var athleteIdPart = parts[athletesIndex + 1];
        var q = athleteIdPart.IndexOf('?');
        var athleteId = q >= 0 ? athleteIdPart[..q] : athleteIdPart;

        if (string.IsNullOrWhiteSpace(athleteId) || !athleteId.All(char.IsDigit))
            throw new InvalidOperationException($"Missing or invalid athlete id in ref: {athleteSeasonRef}");

        // Build base up to *before* "seasons"
        var baseParts = parts.Take(seasonsIndex)
            .Append("athletes")
            .Append(athleteId);

        var path = string.Join('/', baseParts);
        var query = athleteSeasonRef.Query;

        return new Uri(path + query, UriKind.Absolute);
    }

}
