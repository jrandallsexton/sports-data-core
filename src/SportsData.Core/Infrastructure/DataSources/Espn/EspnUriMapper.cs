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
        if (athleteSeasonRef == null)
            throw new ArgumentNullException(nameof(athleteSeasonRef));

        var s = athleteSeasonRef.ToString();
        var parts = s.Split('/');

        // Expect: ... / seasons / {year} / athletes / {athleteId}[?qs]
        var seasonsIndex = Array.IndexOf(parts, "seasons");
        var athletesIndex = Array.IndexOf(parts, "athletes");

        if (seasonsIndex < 0 || athletesIndex < 0 || athletesIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN AthleteSeason ref format: {athleteSeasonRef}");

        var athleteIdPart = parts[athletesIndex + 1];
        var q = athleteIdPart.IndexOf('?');
        var athleteId = q >= 0 ? athleteIdPart[..q] : athleteIdPart;

        // NEW: Use TryParse to allow negative IDs (like -6952)
        if (!int.TryParse(athleteId, out _))
            throw new InvalidOperationException($"Missing or invalid athlete id in ref: {athleteSeasonRef}");

        // Build base path to athlete root
        var baseParts = parts.Take(seasonsIndex)
            .Append("athletes")
            .Append(athleteId);

        var path = string.Join('/', baseParts);
        var query = athleteSeasonRef.Query;

        return new Uri(path + query, UriKind.Absolute);
    }

    public static Uri SeasonTypeWeekToSeasonType(Uri weekRef)
    {
        if (weekRef == null) throw new ArgumentNullException(nameof(weekRef));

        var s = weekRef.ToString();
        var parts = s.Split('/');

        // Expect: ... / seasons / {year} / types / {typeId} / weeks / {weekId}[?qs]
        var seasonsIndex = Array.IndexOf(parts, "seasons");
        var typesIndex = Array.IndexOf(parts, "types");
        var weeksIndex = Array.IndexOf(parts, "weeks");

        if (seasonsIndex < 0 || typesIndex < 0 || weeksIndex < 0 || weeksIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN SeasonTypeWeek ref format: {weekRef}");

        var seasonYearPart = parts[seasonsIndex + 1];
        var typeIdPart = parts[typesIndex + 1];

        if (!seasonYearPart.All(char.IsDigit))
            throw new InvalidOperationException($"Invalid season year in ref: {weekRef}");

        if (!typeIdPart.All(char.IsDigit))
            throw new InvalidOperationException($"Invalid type id in ref: {weekRef}");

        // FIXED: include the {typeId} segment
        var baseParts = parts.Take(weeksIndex); // stop before "weeks"
        var path = string.Join('/', baseParts);
        var query = weekRef.Query;

        return new Uri(path + query, UriKind.Absolute);
    }

    public static Uri SeasonTypeToSeason(Uri seasonTypeRef)
    {
        if (seasonTypeRef == null)
            throw new ArgumentNullException(nameof(seasonTypeRef));

        var s = seasonTypeRef.ToString();
        var parts = s.Split('/');

        // Expect: ... / seasons / {year} / types / {typeId}[?qs]
        var seasonsIndex = Array.IndexOf(parts, "seasons");
        var typesIndex = Array.IndexOf(parts, "types");

        if (seasonsIndex < 0 || typesIndex < 0 || seasonsIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN SeasonType ref format: {seasonTypeRef}");

        var yearPart = parts[seasonsIndex + 1];
        var q = yearPart.IndexOf('?');
        var seasonYear = q >= 0 ? yearPart[..q] : yearPart;

        if (!int.TryParse(seasonYear, out _))
            throw new InvalidOperationException($"Missing or invalid season year in ref: {seasonTypeRef}");

        // Build base path to season root
        var baseParts = parts.Take(seasonsIndex + 2); // includes "seasons/{year}"
        var path = string.Join('/', baseParts);
        var query = seasonTypeRef.Query;

        return new Uri(path + query, UriKind.Absolute);
    }

    public static Uri CompetitionRefToCompetitionStatusRef(Uri competitionRef)
    {
        if (competitionRef == null)
            throw new ArgumentNullException(nameof(competitionRef));

        var s = competitionRef.ToString();
        var parts = s.Split('/');

        // Expect: ... / events / {eventId} / competitions / {competitionId}[?qs]
        var eventsIndex = Array.IndexOf(parts, "events");
        var competitionsIndex = Array.IndexOf(parts, "competitions");

        if (eventsIndex < 0 || competitionsIndex < 0 || competitionsIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN Competition ref format: {competitionRef}");

        // Extract competitionId (strip query if present)
        var competitionIdPart = parts[competitionsIndex + 1];
        var q = competitionIdPart.IndexOf('?');
        var competitionId = q >= 0 ? competitionIdPart[..q] : competitionIdPart;

        if (string.IsNullOrWhiteSpace(competitionId) || !competitionId.All(char.IsDigit))
            throw new InvalidOperationException($"Missing or invalid competition id in ref: {competitionRef}");

        // Append "status" after competition ID
        var baseParts = parts.Take(competitionsIndex + 2)
            .Append("status");

        var path = string.Join('/', baseParts);
        var query = competitionRef.Query;

        return new Uri(path + query, UriKind.Absolute);
    }

    public static Uri CompetitionRefToCompetitionPlaysRef(Uri competitionRef, int limit = 25)
    {
        if (competitionRef == null)
            throw new ArgumentNullException(nameof(competitionRef));

        var s = competitionRef.ToString();
        var parts = s.Split('/');

        // Expect: ... / events / {eventId} / competitions / {competitionId}[?qs]
        var eventsIndex = Array.IndexOf(parts, "events");
        var competitionsIndex = Array.IndexOf(parts, "competitions");

        if (eventsIndex < 0 || competitionsIndex < 0 || competitionsIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN Competition ref format: {competitionRef}");

        // Extract competitionId (strip query if present)
        var competitionIdPart = parts[competitionsIndex + 1];
        var q = competitionIdPart.IndexOf('?');
        var competitionId = q >= 0 ? competitionIdPart[..q] : competitionIdPart;

        if (string.IsNullOrWhiteSpace(competitionId) || !competitionId.All(char.IsDigit))
            throw new InvalidOperationException($"Missing or invalid competition id in ref: {competitionRef}");

        // Append "plays" after competition ID
        var baseParts = parts.Take(competitionsIndex + 2).Append("plays");
        var path = string.Join('/', baseParts);

        // Preserve existing query and append ?limit= or &limit=
        var existingQuery = competitionRef.Query; // includes leading "?" if present
        var limitQuery = string.IsNullOrWhiteSpace(existingQuery)
            ? $"?limit={limit}"
            : $"{existingQuery}&limit={limit}";

        return new Uri(path + limitQuery, UriKind.Absolute);
    }

    public static Uri SeasonPollWeekRefToSeasonPollRef(Uri seasonPollWeekRef)
    {
        if (seasonPollWeekRef == null) throw new ArgumentNullException(nameof(seasonPollWeekRef));

        var s = seasonPollWeekRef.GetLeftPart(UriPartial.Path); // removes ?query
        var parts = s.Split('/');

        // Expect: ... / seasons / {year} / types / {typeId} / weeks / {weekId} / rankings / {rankingId}
        var seasonsIndex = Array.IndexOf(parts, "seasons");
        var typesIndex = Array.IndexOf(parts, "types");
        var weeksIndex = Array.IndexOf(parts, "weeks");
        var rankingsIndex = Array.IndexOf(parts, "rankings");

        if (seasonsIndex < 0 || typesIndex < 0 || weeksIndex < 0 || rankingsIndex < 0 || rankingsIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN SeasonPollWeek ref format: {seasonPollWeekRef}");

        var seasonYearPart = parts[seasonsIndex + 1];
        var rankingIdPart = parts[rankingsIndex + 1];

        if (!seasonYearPart.All(char.IsDigit))
            throw new InvalidOperationException($"Invalid season year in ref: {seasonPollWeekRef}");

        if (!rankingIdPart.All(char.IsDigit))
            throw new InvalidOperationException($"Invalid ranking id in ref: {seasonPollWeekRef}");

        // Keep everything up to /seasons/{year}
        var baseParts = parts.Take(seasonsIndex + 2); // includes "seasons" and seasonYear
        var finalPath = string.Join('/', baseParts.Concat(new[] { "rankings", rankingIdPart }));

        return new Uri(finalPath, UriKind.Absolute); // do NOT append query string
    }


}
