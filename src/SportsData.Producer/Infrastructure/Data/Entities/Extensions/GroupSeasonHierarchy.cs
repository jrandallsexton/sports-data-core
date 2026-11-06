using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Infrastructure.Data.Common;

using System.Text.RegularExpressions;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

/// <summary>
/// Conference-membership helpers shared by GroupSeasonDocumentProcessor and
/// TeamSeasonDocumentProcessor. Replaces the hand-run
/// sql/pgsql/groupSeasonHierarchy.sql: the abbreviation patch and the
/// GroupSeasonMap path build now happen when the rows are written.
/// </summary>
public static partial class GroupSeasonHierarchy
{
    public const string UnknownAbbreviation = "UNK";

    // NCAA football's division groups. ESPN serves no abbreviation for them,
    // which is where the 'UNK' rows the old script patched came from. The ids
    // are per league and held for every season 2001-2025.
    private const long EspnNcaaFbsGroupId = 80;
    private const long EspnNcaaFcsGroupId = 81;

    // Bounds the parent walk; the real NCAA depth is 5 (NCAAF|NCAA|fbs|sec|secw).
    private const int MaxDepth = 10;

    [GeneratedRegex(@"/types/(\d+)/groups/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TypeAndGroupPattern();

    /// <summary>
    /// ESPN's abbreviation, or a stable one when ESPN omits it. Only the NCAA
    /// football FBS/FCS nodes are derived; every GroupSeasonMap FBS filter
    /// (GetCompletedFbsContestIds.sql, the matchup queries) matches on "|fbs".
    /// </summary>
    public static string ResolveAbbreviation(EspnGroupSeasonDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Abbreviation) &&
            !string.Equals(dto.Abbreviation, UnknownAbbreviation, StringComparison.OrdinalIgnoreCase))
        {
            return dto.Abbreviation;
        }

        var isNcaaFootball = dto.Ref?.AbsolutePath
            .Contains("/leagues/college-football/", StringComparison.OrdinalIgnoreCase) == true;

        if (isNcaaFootball)
        {
            if (dto.Id == EspnNcaaFbsGroupId) return "fbs";
            if (dto.Id == EspnNcaaFcsGroupId) return "fcs";
        }

        return UnknownAbbreviation;
    }

    /// <summary>
    /// Builds the flattened path ("NCAAF|NCAA|fbs|sec") from the root down to
    /// the group. Always complete for a resolvable group:
    /// GroupSeasonDocumentProcessor refuses to create a group until its parent
    /// exists. Returns null when the group does not exist.
    /// </summary>
    public static async Task<string?> BuildMapAsync(
        TeamSportDataContext dataContext,
        Guid groupSeasonId)
    {
        var segments = new List<string>();
        Guid? currentId = groupSeasonId;

        while (currentId is not null && segments.Count < MaxDepth)
        {
            var node = await dataContext.GroupSeasons
                .AsNoTracking()
                .Where(g => g.Id == currentId)
                .Select(g => new { g.ParentId, g.Abbreviation })
                .FirstOrDefaultAsync();

            if (node is null)
                break;

            segments.Add(node.Abbreviation);
            currentId = node.ParentId;
        }

        if (segments.Count == 0)
            return null;

        segments.Reverse();
        return string.Join('|', segments);
    }

    /// <summary>
    /// True when <paramref name="candidateAncestorId"/> is a strict ancestor of
    /// <paramref name="groupSeasonId"/>. Used to keep the most specific group:
    /// a conference's member list must not overwrite a division assignment.
    /// </summary>
    public static async Task<bool> IsAncestorAsync(
        TeamSportDataContext dataContext,
        Guid candidateAncestorId,
        Guid groupSeasonId)
    {
        Guid? currentId = groupSeasonId;

        for (var depth = 0; depth < MaxDepth && currentId is not null; depth++)
        {
            var parentId = await dataContext.GroupSeasons
                .AsNoTracking()
                .Where(g => g.Id == currentId)
                .Select(g => g.ParentId)
                .FirstOrDefaultAsync();

            if (parentId == candidateAncestorId)
                return true;

            currentId = parentId;
        }

        return false;
    }

    /// <summary>
    /// GroupSeason stores each ESPN group once per season type (types/1 to
    /// types/4). Conference W/L compares GroupSeasonId between two teams, so
    /// every team-season in a season must point at the same variant. Maps
    /// <paramref name="incomingGroupSeasonId"/> to the variant
    /// <paramref name="currentGroupSeasonId"/> already uses; returns the
    /// incoming id unchanged when there is no current group, the variants
    /// already agree, or the matching variant has not been sourced.
    /// </summary>
    public static async Task<Guid> MatchVariantAsync(
        TeamSportDataContext dataContext,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid incomingGroupSeasonId,
        Guid? currentGroupSeasonId)
    {
        if (currentGroupSeasonId is null || currentGroupSeasonId == incomingGroupSeasonId)
            return incomingGroupSeasonId;

        var incomingUrl = await GetSourceUrlAsync(dataContext, incomingGroupSeasonId);
        var currentUrl = await GetSourceUrlAsync(dataContext, currentGroupSeasonId.Value);

        if (incomingUrl is null || currentUrl is null)
            return incomingGroupSeasonId;

        var incomingMatch = TypeAndGroupPattern().Match(incomingUrl);
        var currentMatch = TypeAndGroupPattern().Match(currentUrl);

        if (!incomingMatch.Success || !currentMatch.Success)
            return incomingGroupSeasonId;

        var incomingType = incomingMatch.Groups[1].Value;
        var currentType = currentMatch.Groups[1].Value;

        if (incomingType == currentType)
            return incomingGroupSeasonId;

        var targetUrl = TypeAndGroupPattern().Replace(
            incomingUrl,
            $"/types/{currentType}/groups/{incomingMatch.Groups[2].Value}",
            1);

        var targetHash = externalRefIdentityGenerator.Generate(new Uri(targetUrl)).UrlHash;

        var targetId = await dataContext.Set<GroupSeasonExternalId>()
            .AsNoTracking()
            .Where(x => x.SourceUrlHash == targetHash)
            .Select(x => (Guid?)x.GroupSeasonId)
            .FirstOrDefaultAsync();

        return targetId ?? incomingGroupSeasonId;
    }

    private static Task<string?> GetSourceUrlAsync(TeamSportDataContext dataContext, Guid groupSeasonId) =>
        dataContext.Set<GroupSeasonExternalId>()
            .AsNoTracking()
            .Where(x => x.GroupSeasonId == groupSeasonId)
            .Select(x => x.SourceUrl)
            .FirstOrDefaultAsync();
}
