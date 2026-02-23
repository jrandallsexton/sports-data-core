using System;
using System.Linq;

namespace SportsData.Core.Infrastructure.DataSources.Espn;

public static class EspnUriMapper
{
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

        return new Uri(path, UriKind.Absolute);
    }

    public static Uri CompetitionCompetitorRefToCompetitionRef(Uri competitionCompetitorRef)
    {
        if (competitionCompetitorRef is null)
            throw new ArgumentNullException(nameof(competitionCompetitorRef));

        var segments = competitionCompetitorRef.Segments;

        // Find /events/{eventId}
        var evtIdx = Array.FindIndex(segments, s => s.Equals("events/", StringComparison.OrdinalIgnoreCase));
        if (evtIdx < 0 || evtIdx + 1 >= segments.Length)
            throw new ArgumentException("URI does not contain an '/events/{id}' segment.", nameof(competitionCompetitorRef));

        // Find /competitions/{competitionId} AFTER events/{id}
        var compIdx = Array.FindIndex(segments, evtIdx + 2, s => s.Equals("competitions/", StringComparison.OrdinalIgnoreCase));
        if (compIdx < 0 || compIdx + 1 >= segments.Length)
            throw new ArgumentException("URI does not contain a '/competitions/{id}' segment.", nameof(competitionCompetitorRef));

        // Rebuild up to events/{eventId}/competitions/{competitionId}
        var prefix = string.Concat(segments.Take(evtIdx));            // keep leading path as-is
        var eventsSeg = "events/";                                    // normalize segment casing
        var eventIdSeg = segments[evtIdx + 1].TrimEnd('/');

        var competitions = "competitions/";
        var competitionIdSeg = segments[compIdx + 1].TrimEnd('/');

        var path = $"{prefix}{eventsSeg}{eventIdSeg}/{competitions}{competitionIdSeg}";
        var uriString = $"{competitionCompetitorRef.Scheme}://{competitionCompetitorRef.Authority}{path}".TrimEnd('/');

        return new Uri(uriString);
    }

    public static Uri CompetitionCompetitorScoreRefToCompetitionCompetitorRef(Uri competitionCompetitorScoreRef)
    {
        if (competitionCompetitorScoreRef is null)
            throw new ArgumentNullException(nameof(competitionCompetitorScoreRef));

        return BuildCompetitionCompetitorRefFrom(competitionCompetitorScoreRef, nameof(competitionCompetitorScoreRef));
    }

    public static Uri CompetitionCompetitorStatisticsRefToCompetitionCompetitorRef(Uri competitionCompetitorStatisticsRef)
    {
        if (competitionCompetitorStatisticsRef is null)
            throw new ArgumentNullException(nameof(competitionCompetitorStatisticsRef));

        return BuildCompetitionCompetitorRefFrom(competitionCompetitorStatisticsRef, nameof(competitionCompetitorStatisticsRef));
    }

    public static Uri CompetitionLeadersRefToCompetitionRef(Uri competitionLeadersRef)
    {
        if (competitionLeadersRef == null)
            throw new ArgumentNullException(nameof(competitionLeadersRef));

        // Remove query string and trailing segments after "/competitions/{id}"
        var uri = competitionLeadersRef.GetLeftPart(UriPartial.Path);

        // Find "/leaders" segment and trim it off
        var trimmed = uri;
        if (trimmed.EndsWith("/leaders", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/leaders".Length];

        return new Uri(trimmed);
    }

    public static Uri CompetitionLineScoreRefToCompetitionCompetitorRef(Uri competitionLineScoreRef)
    {
        if (competitionLineScoreRef is null)
            throw new ArgumentNullException(nameof(competitionLineScoreRef));

        return BuildCompetitionCompetitorRefFrom(competitionLineScoreRef, nameof(competitionLineScoreRef));
    }

    /// <summary>
    /// Extracts and normalizes URIs containing events/{id}/competitions/{id}/competitors/{id} pattern.
    /// </summary>
    /// <param name="sourceUri">The source URI to normalize.</param>
    /// <param name="parameterName">The parameter name for exception messages.</param>
    /// <returns>A normalized URI with canonical segment names.</returns>
    private static Uri BuildCompetitionCompetitorRefFrom(Uri sourceUri, string parameterName)
    {
        var segments = sourceUri.Segments;

        // locate /events/{eventId}
        var evtIdx = Array.FindIndex(segments, s => s.Equals("events/", StringComparison.OrdinalIgnoreCase));
        if (evtIdx < 0 || evtIdx + 1 >= segments.Length)
            throw new ArgumentException("URI does not contain an '/events/{id}' segment.", parameterName);

        // locate /competitions/{competitionId} AFTER events/{id}
        var compIdx = Array.FindIndex(segments, evtIdx + 2, s => s.Equals("competitions/", StringComparison.OrdinalIgnoreCase));
        if (compIdx < 0 || compIdx + 1 >= segments.Length)
            throw new ArgumentException("URI does not contain a '/competitions/{id}' segment.", parameterName);

        // locate /competitors/{competitorId} AFTER competitions/{id}
        var competitorIdx = Array.FindIndex(segments, compIdx + 2, s => s.Equals("competitors/", StringComparison.OrdinalIgnoreCase));
        if (competitorIdx < 0 || competitorIdx + 1 >= segments.Length)
            throw new ArgumentException("URI does not contain a '/competitors/{id}' segment.", parameterName);

        // normalize segment names; keep IDs as-is
        var prefix = string.Concat(segments.Take(evtIdx));           // everything before "events/"
        var eventsSeg = "events/";
        var eventIdSeg = segments[evtIdx + 1].TrimEnd('/');

        var competitions = "competitions/";
        var competitionId = segments[compIdx + 1].TrimEnd('/');

        var competitors = "competitors/";
        var competitorId = segments[competitorIdx + 1].TrimEnd('/');

        var path = $"{prefix}{eventsSeg}{eventIdSeg}/{competitions}{competitionId}/{competitors}{competitorId}";
        var uriString = $"{sourceUri.Scheme}://{sourceUri.Authority}{path}".TrimEnd('/');

        return new Uri(uriString);
    }

    public static Uri CompetitionLineScoreRefToCompetitionRef(Uri competitionLineScoreRef)
    {
        if (competitionLineScoreRef is null)
            throw new ArgumentNullException(nameof(competitionLineScoreRef));

        var segments = competitionLineScoreRef.Segments;

        // find "/competitions/" (case-insensitive) and ensure there is an id after it
        var idx = Array.FindIndex(segments, s => s.Equals("competitions/", StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx + 1 >= segments.Length)
            throw new ArgumentException("URI does not contain a '/competitions/{id}' segment.", nameof(competitionLineScoreRef));

        // rebuild path up to competitions/{id}
        // keep the prefix segments as-is, normalize only the "competitions/" segment
        var prefix = string.Concat(segments.Take(idx));
        var competitionsSeg = "competitions/"; // normalized
        var idSeg = segments[idx + 1].TrimEnd('/');

        var path = $"{prefix}{competitionsSeg}{idSeg}";

        // compose final uri (drop query/fragment, trim trailing slash)
        var uriString = $"{competitionLineScoreRef.Scheme}://{competitionLineScoreRef.Authority}{path}".TrimEnd('/');

        return new Uri(uriString);
    }

    public static Uri CompetitionRefToCompetitionDrivesRef(Uri competitionRef, int limit = 25)
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
        var baseParts = parts.Take(competitionsIndex + 2).Append("drives");
        var path = string.Join('/', baseParts);

        // Preserve existing query and append ?limit= or &limit=
        var existingQuery = competitionRef.Query; // includes leading "?" if present
        var limitQuery = string.IsNullOrWhiteSpace(existingQuery)
            ? $"?limit={limit}"
            : $"{existingQuery}&limit={limit}";

        return new Uri(path + limitQuery, UriKind.Absolute);
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

        return new Uri(path, UriKind.Absolute);
    }

    public static Uri CompetitionRefToContestRef(Uri competitionRef)
    {
        if (competitionRef is null)
            throw new ArgumentNullException(nameof(competitionRef));

        var segments = competitionRef.Segments;

        // Find "/events/" segment (case-insensitive) and ensure an id follows
        var idx = Array.FindIndex(segments, s => s.Equals("events/", StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx + 1 >= segments.Length)
            throw new ArgumentException("URI does not contain an '/events/{id}' segment.", nameof(competitionRef));

        // Rebuild path up to events/{id}
        var prefix = string.Concat(segments.Take(idx));
        var eventsSeg = "events/";                       // normalize casing
        var eventIdSeg = segments[idx + 1].TrimEnd('/');  // keep original id

        var path = $"{prefix}{eventsSeg}{eventIdSeg}";

        var uriString = $"{competitionRef.Scheme}://{competitionRef.Authority}{path}".TrimEnd('/');

        return new Uri(uriString);
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

        return new Uri(path, UriKind.Absolute);
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

        return new Uri(path, UriKind.Absolute);
    }

    public static Uri SeasonAwardToAwardRef(Uri seasonAwardRef)
    {
        if (seasonAwardRef == null)
            throw new ArgumentNullException(nameof(seasonAwardRef));

        var s = seasonAwardRef.ToString();
        var parts = s.Split('/');

        // Expect: ... / seasons / {year} / awards / {awardId}[?qs]
        var seasonsIndex = Array.IndexOf(parts, "seasons");
        var awardsIndex = Array.IndexOf(parts, "awards");

        if (seasonsIndex < 0 || awardsIndex < 0 || awardsIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN SeasonAward ref format: {seasonAwardRef}");

        // Extract awardId (strip query if present)
        var awardIdPart = parts[awardsIndex + 1];
        var q = awardIdPart.IndexOf('?');
        var awardId = q >= 0 ? awardIdPart[..q] : awardIdPart;

        if (string.IsNullOrWhiteSpace(awardId) || !awardId.All(char.IsDigit))
            throw new InvalidOperationException($"Missing or invalid award id in ref: {seasonAwardRef}");

        // Build base path to award root (skip seasons/{year} segment)
        var baseParts = parts.Take(seasonsIndex)
            .Append("awards")
            .Append(awardId);

        var path = string.Join('/', baseParts);

        return new Uri(path, UriKind.Absolute);
    }

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

        return new Uri(path, UriKind.Absolute);
    }

    /// <summary>
    /// Maps a TeamSeason child URI (e.g., statistics, leaders, record) back to the TeamSeason URI.
    /// Common helper for all TeamSeason child resources.
    /// </summary>
    private static Uri TeamSeasonChildToTeamSeasonRef(Uri childUri, string parameterName)
    {
        if (childUri == null) throw new ArgumentNullException(parameterName);

        var path = childUri.GetLeftPart(UriPartial.Path);
        var parts = path.Split('/');

        // Expect: ... / seasons / {year} / teams / {teamId} / {childResource} / {id?}
        var seasonsIndex = Array.IndexOf(parts, "seasons");
        var teamsIndex = Array.IndexOf(parts, "teams");

        if (seasonsIndex < 0 || teamsIndex < 0 || teamsIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN TeamSeason child ref format: {childUri}");

        var teamIdPart = parts[teamsIndex + 1];

        if (string.IsNullOrWhiteSpace(teamIdPart) || !teamIdPart.All(char.IsDigit))
            throw new InvalidOperationException($"Missing or invalid team id in ref: {childUri}");

        // Build path up to teams/{teamId}
        var baseParts = parts.Take(teamsIndex + 2); // includes .../teams/{teamId}
        var result = string.Join('/', baseParts);

        return new Uri(result, UriKind.Absolute);
    }

    public static Uri TeamSeasonStatisticsRefToTeamSeasonRef(Uri statisticsRef)
        => TeamSeasonChildToTeamSeasonRef(statisticsRef, nameof(statisticsRef));

    public static Uri TeamSeasonLeadersRefToTeamSeasonRef(Uri leadersRef)
        => TeamSeasonChildToTeamSeasonRef(leadersRef, nameof(leadersRef));

    public static Uri TeamSeasonRankRefToTeamSeasonRef(Uri rankRef)
        => TeamSeasonChildToTeamSeasonRef(rankRef, nameof(rankRef));

    public static Uri TeamSeasonRecordRefToTeamSeasonRef(Uri recordRef)
        => TeamSeasonChildToTeamSeasonRef(recordRef, nameof(recordRef));

    public static Uri TeamSeasonRecordAtsRefToTeamSeasonRef(Uri recordAtsRef)
        => TeamSeasonChildToTeamSeasonRef(recordAtsRef, nameof(recordAtsRef));

    public static Uri TeamSeasonProjectionRefToTeamSeasonRef(Uri projectionRef)
        => TeamSeasonChildToTeamSeasonRef(projectionRef, nameof(projectionRef));

    public static Uri TeamSeasonAwardRefToTeamSeasonRef(Uri awardRef)
        => TeamSeasonChildToTeamSeasonRef(awardRef, nameof(awardRef));

    public static Uri CompetitionBroadcastRefToCompetitionRef(Uri broadcastRef)
    {
        if (broadcastRef == null) throw new ArgumentNullException(nameof(broadcastRef));

        var path = broadcastRef.GetLeftPart(UriPartial.Path);

        // Trim "/broadcasts" or "/broadcasts/{id}"
        var trimmed = path;
        var broadcastsIndex = trimmed.LastIndexOf("/broadcasts", StringComparison.OrdinalIgnoreCase);
        if (broadcastsIndex > 0)
            trimmed = trimmed[..broadcastsIndex];

        return new Uri(trimmed, UriKind.Absolute);
    }

    public static Uri CompetitionPlayRefToCompetitionRef(Uri playRef)
    {
        if (playRef == null) throw new ArgumentNullException(nameof(playRef));

        var path = playRef.GetLeftPart(UriPartial.Path);

        // Trim "/plays" or "/plays/{id}"
        var trimmed = path;
        var playsIndex = trimmed.LastIndexOf("/plays", StringComparison.OrdinalIgnoreCase);
        if (playsIndex > 0)
            trimmed = trimmed[..playsIndex];

        return new Uri(trimmed, UriKind.Absolute);
    }

    public static Uri CompetitionPredictionRefToCompetitionRef(Uri predictionRef)
    {
        if (predictionRef == null) throw new ArgumentNullException(nameof(predictionRef));

        var path = predictionRef.GetLeftPart(UriPartial.Path);

        // Trim "/prediction" or "/predictions"
        var trimmed = path;
        if (trimmed.EndsWith("/prediction", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/prediction".Length];
        else if (trimmed.EndsWith("/predictions", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/predictions".Length];

        return new Uri(trimmed, UriKind.Absolute);
    }

    public static Uri CompetitionStatusRefToCompetitionRef(Uri statusRef)
    {
        if (statusRef == null) throw new ArgumentNullException(nameof(statusRef));

        var path = statusRef.GetLeftPart(UriPartial.Path);

        // Trim "/status"
        var trimmed = path;
        if (trimmed.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/status".Length];

        return new Uri(trimmed, UriKind.Absolute);
    }

    public static Uri CompetitionSituationRefToCompetitionRef(Uri situationRef)
    {
        if (situationRef == null) throw new ArgumentNullException(nameof(situationRef));

        var path = situationRef.GetLeftPart(UriPartial.Path);

        // Trim "/situation"
        var trimmed = path;
        if (trimmed.EndsWith("/situation", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/situation".Length];

        return new Uri(trimmed, UriKind.Absolute);
    }

    public static Uri CompetitionDriveRefToCompetitionRef(Uri driveRef)
    {
        if (driveRef == null) throw new ArgumentNullException(nameof(driveRef));

        var path = driveRef.GetLeftPart(UriPartial.Path);

        // Trim "/drives/{id}" or "/drives"
        var trimmed = path;
        if (trimmed.Contains("/drives", StringComparison.OrdinalIgnoreCase))
        {
            var drivesIndex = trimmed.LastIndexOf("/drives", StringComparison.OrdinalIgnoreCase);
            trimmed = trimmed[..drivesIndex];
        }

        return new Uri(trimmed, UriKind.Absolute);
    }

    public static Uri CompetitionOddsRefToCompetitionRef(Uri oddsRef)
    {
        if (oddsRef == null) throw new ArgumentNullException(nameof(oddsRef));

        var path = oddsRef.GetLeftPart(UriPartial.Path);

        // Trim "/odds" or "/odds/{id}"
        var trimmed = path;
        if (trimmed.Contains("/odds", StringComparison.OrdinalIgnoreCase))
        {
            var oddsIndex = trimmed.LastIndexOf("/odds", StringComparison.OrdinalIgnoreCase);
            trimmed = trimmed[..oddsIndex];
        }

        return new Uri(trimmed, UriKind.Absolute);
    }

    public static Uri CompetitionPowerIndexRefToCompetitionRef(Uri powerIndexRef)
    {
        if (powerIndexRef == null) throw new ArgumentNullException(nameof(powerIndexRef));

        var path = powerIndexRef.GetLeftPart(UriPartial.Path);

        // Trim "/powerindex" or "/power-index"
        var trimmed = path;
        if (trimmed.EndsWith("/powerindex", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/powerindex".Length];
        else if (trimmed.EndsWith("/power-index", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/power-index".Length];

        return new Uri(trimmed, UriKind.Absolute);
    }

    public static Uri AthleteSeasonStatisticsRefToAthleteSeasonRef(Uri statisticsRef)
    {
        if (statisticsRef == null) throw new ArgumentNullException(nameof(statisticsRef));

        var path = statisticsRef.GetLeftPart(UriPartial.Path);
        var parts = path.Split('/');

        // Expect: ... / seasons / {year} / athletes / {athleteId} / statistics / {id}
        var seasonsIndex = Array.IndexOf(parts, "seasons");
        var athletesIndex = Array.IndexOf(parts, "athletes");

        if (seasonsIndex < 0 || athletesIndex < 0 || athletesIndex + 1 >= parts.Length)
            throw new InvalidOperationException($"Unexpected ESPN AthleteSeason statistics ref format: {statisticsRef}");

        var athleteId = parts[athletesIndex + 1];

        if (string.IsNullOrWhiteSpace(athleteId))
            throw new InvalidOperationException($"Missing athlete id in ref: {statisticsRef}");

        // Build path up to athletes/{athleteId}
        var baseParts = parts.Take(athletesIndex + 2); // includes .../athletes/{athleteId}
        var result = string.Join('/', baseParts);

        return new Uri(result, UriKind.Absolute);
    }

    public static Uri CoachSeasonRecordRefToCoachSeasonRef(Uri recordRef)
    {
        if (recordRef == null) throw new ArgumentNullException(nameof(recordRef));

        var path = recordRef.GetLeftPart(UriPartial.Path);

        // Trim "/record"
        var trimmed = path;
        if (trimmed.EndsWith("/record", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/record".Length];

        return new Uri(trimmed, UriKind.Absolute);
    }

    public static Uri CoachRecordRefToCoachRef(Uri recordRef)
    {
        if (recordRef == null) throw new ArgumentNullException(nameof(recordRef));

        var path = recordRef.GetLeftPart(UriPartial.Path);

        // Trim "/record"
        var trimmed = path;
        if (trimmed.EndsWith("/record", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/record".Length];

        return new Uri(trimmed, UriKind.Absolute);
    }
}
