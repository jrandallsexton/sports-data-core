using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.UI.Leagues.Mapping;

/// <summary>
/// Maps the canonical Producer-side <see cref="LeagueMatchupDto"/> into
/// the API-side <see cref="LeagueWeekMatchupsDto.MatchupForPickDto"/>
/// shape consumed by UI matchup cards. Pure: only canonical fields.
///
/// League-context fields — Predictions, AiWinnerFranchiseSeasonId,
/// IsPreviewAvailable, IsPreviewReviewed, HeadLine — are NOT set here;
/// callers layer those on top from their own data sources.
/// </summary>
public static class MatchupForPickDtoMapper
{
    /// <summary>
    /// Mutates an existing matchup in-place with all canonical-derived
    /// fields. Used by the league/week query handler which already has
    /// a partially-populated DTO from the league's PickemGroupMatchup row.
    /// </summary>
    public static void ApplyCanonical(
        LeagueWeekMatchupsDto.MatchupForPickDto matchup,
        LeagueMatchupDto canonical)
    {
        // Pass both wire-shape status fields through verbatim — no
        // transformation, no enum parse. Same dual-field shape the rest of
        // the picks-page wire surface uses (canonical Matchup, SignalR
        // ContestStatusChanged).
        matchup.Status = canonical.Status;
        matchup.StatusDescription = canonical.StatusDescription;
        matchup.Broadcasts = canonical.Broadcasts;

        // Away team
        matchup.Away = canonical.Away ?? matchup.Away;
        matchup.AwayShort = canonical.AwayShort ?? matchup.AwayShort;
        matchup.AwayFranchiseSeasonId = canonical.AwayFranchiseSeasonId;
        matchup.AwayLogoUri = canonical.AwayLogoUri ?? matchup.AwayLogoUri;
        matchup.AwayLogoUriDark = canonical.AwayLogoUriDark;
        matchup.AwaySlug = canonical.AwaySlug ?? matchup.AwaySlug;
        matchup.AwayColor = canonical.AwayColor ?? matchup.AwayColor;
        matchup.AwayWins = canonical.AwayWins;
        matchup.AwayLosses = canonical.AwayLosses;
        matchup.AwayConferenceWins = canonical.AwayConferenceWins;
        matchup.AwayConferenceLosses = canonical.AwayConferenceLosses;
        matchup.AwayRank = canonical.AwayRank;

        // Home team
        matchup.Home = canonical.Home ?? matchup.Home;
        matchup.HomeShort = canonical.HomeShort ?? matchup.HomeShort;
        matchup.HomeFranchiseSeasonId = canonical.HomeFranchiseSeasonId;
        matchup.HomeLogoUri = canonical.HomeLogoUri ?? matchup.HomeLogoUri;
        matchup.HomeLogoUriDark = canonical.HomeLogoUriDark;
        matchup.HomeSlug = canonical.HomeSlug ?? matchup.HomeSlug;
        matchup.HomeColor = canonical.HomeColor ?? matchup.HomeColor;
        matchup.HomeWins = canonical.HomeWins;
        matchup.HomeLosses = canonical.HomeLosses;
        matchup.HomeConferenceWins = canonical.HomeConferenceWins;
        matchup.HomeConferenceLosses = canonical.HomeConferenceLosses;
        matchup.HomeRank = canonical.HomeRank;

        // Odds — round to one decimal for display.
        matchup.SpreadCurrent = canonical.SpreadCurrent.HasValue
            ? (decimal)Math.Round(canonical.SpreadCurrent.Value, 1, MidpointRounding.AwayFromZero)
            : null;

        matchup.SpreadOpen = canonical.SpreadOpen.HasValue
            ? (decimal)Math.Round(canonical.SpreadOpen.Value, 1, MidpointRounding.AwayFromZero)
            : null;

        matchup.OverUnderCurrent = canonical.OverUnderCurrent.HasValue
            ? (decimal)Math.Round(canonical.OverUnderCurrent.Value, 1, MidpointRounding.AwayFromZero)
            : null;

        matchup.OverUnderOpen = canonical.OverUnderOpen.HasValue
            ? (decimal)Math.Round(canonical.OverUnderOpen.Value, 1, MidpointRounding.AwayFromZero)
            : null;

        // Venue
        matchup.Venue = canonical.Venue ?? matchup.Venue;
        matchup.VenueCity = canonical.VenueCity ?? matchup.VenueCity;
        matchup.VenueState = canonical.VenueState ?? matchup.VenueState;

        // Result
        matchup.IsComplete = canonical.CompletedUtc.HasValue;
        matchup.AwayScore = canonical.AwayScore;
        matchup.HomeScore = canonical.HomeScore;
        matchup.WinnerFranchiseSeasonId = canonical.WinnerFranchiseSeasonId;
        matchup.SpreadWinnerFranchiseSeasonId = canonical.SpreadWinnerFranchiseSeasonId;
        matchup.OverUnderResult = canonical.OverUnderResult.HasValue
            ? (OverUnderPick)canonical.OverUnderResult.Value
            : null;
        matchup.CompletedUtc = canonical.CompletedUtc;

        matchup.StreamScheduledTimeUtc = canonical.StreamScheduledTimeUtc;

        // MLB only — null for non-MLB leagues; UI conditionally renders.
        matchup.HomeProbablePitcher = canonical.HomeProbablePitcher;
        matchup.AwayProbablePitcher = canonical.AwayProbablePitcher;
    }

    /// <summary>
    /// Builds a fresh <see cref="LeagueWeekMatchupsDto.MatchupForPickDto"/>
    /// populated from canonical fields only. Used by the admin debug
    /// endpoint where there's no league context to merge with.
    /// </summary>
    public static LeagueWeekMatchupsDto.MatchupForPickDto FromCanonical(LeagueMatchupDto canonical)
    {
        var matchup = new LeagueWeekMatchupsDto.MatchupForPickDto
        {
            ContestId = canonical.ContestId,
            StartDateUtc = canonical.StartDateUtc,
        };
        ApplyCanonical(matchup, canonical);
        return matchup;
    }
}
