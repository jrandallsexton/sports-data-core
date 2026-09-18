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
    /// <param name="sport">
    /// Required, with no default: the live-state mapping is sport-shaped
    /// (football gets "Q3", baseball an inning number), so a defaulted
    /// sport would silently give an MLB caller football-shaped periods.
    /// </param>
    /// <summary>
    /// A side whose whole record is zero carries no information: either the
    /// snapshot predates <c>MatchupRecordSnapshots</c> and was never written,
    /// or the team genuinely has not played. Both want the canonical value.
    /// </summary>
    private static bool IsEmptyRecord(int wins, int losses, int conferenceWins, int conferenceLosses) =>
        wins == 0 && losses == 0 && conferenceWins == 0 && conferenceLosses == 0;

    public static void ApplyCanonical(
        LeagueWeekMatchupsDto.MatchupForPickDto matchup,
        LeagueMatchupDto canonical,
        Sport sport)
    {
        // Pass both wire-shape status fields through verbatim — no
        // transformation, no enum parse. Same dual-field shape the rest of
        // the picks-page wire surface uses (canonical Matchup, SignalR
        // ContestStatusChanged).
        matchup.Status = canonical.Status;
        matchup.StatusDescription = canonical.StatusDescription;
        matchup.Broadcasts = canonical.Broadcasts;

        // Live game state at rest, so a client arriving mid-game — or
        // sitting through a gap in the SignalR stream (commercial break,
        // halftime) — renders a complete live card instead of waiting for
        // the next play. Per-play SignalR events still drive real time and
        // overwrite these on arrival.
        // Period is stored as a bare number but the clients render the
        // football string SignalR already sends ("Q3"), while baseball
        // reads the inning as a number. Format to match each wire shape so
        // a REST-populated card is indistinguishable from a SignalR one.
        var isBaseball = sport == Sport.BaseballMlb;
        matchup.Period = !isBaseball && canonical.Period is > 0
            ? $"Q{canonical.Period}"
            : null;
        matchup.Inning = isBaseball ? canonical.Period : null;
        matchup.Clock = canonical.Clock;
        matchup.PossessionFranchiseSeasonId = canonical.PossessionFranchiseSeasonId;
        matchup.LastPlayId = canonical.LastPlayId;
        matchup.LastPlayDescription = canonical.LastPlayDescription;
        matchup.Down = canonical.Down;
        matchup.Distance = canonical.Distance;
        matchup.BallOnYardLine = canonical.BallOnYardLine;

        // Away team
        matchup.Away = canonical.Away ?? matchup.Away;
        matchup.AwayShort = canonical.AwayShort ?? matchup.AwayShort;
        matchup.AwayShortName = canonical.AwayShortName ?? matchup.AwayShortName;
        matchup.AwayFranchiseSeasonId = canonical.AwayFranchiseSeasonId;
        matchup.AwayLogoUri = canonical.AwayLogoUri ?? matchup.AwayLogoUri;
        matchup.AwayLogoUriDark = canonical.AwayLogoUriDark;
        matchup.AwaySlug = canonical.AwaySlug ?? matchup.AwaySlug;
        matchup.AwayColor = canonical.AwayColor ?? matchup.AwayColor;
        // Records: the league's own PickemGroupMatchup snapshot wins, and the
        // caller has already put it on the DTO. Canonical is the fallback for
        // rows the snapshot never covered — MatchupRecordSnapshots added the
        // eight columns with defaultValue 0 and no backfill, and an
        // already-generated week is not re-entered by MatchupScheduleProcessor
        // unless it is refreshed, so pre-migration rows keep that 0 forever
        // (111 of them in prod, all 2025). Falling back only when the whole
        // side is zero costs nothing at a genuine 0-0 season opener, where
        // both sources agree anyway.
        if (IsEmptyRecord(matchup.AwayWins, matchup.AwayLosses, matchup.AwayConferenceWins, matchup.AwayConferenceLosses))
        {
            matchup.AwayWins = canonical.AwayWins;
            matchup.AwayLosses = canonical.AwayLosses;
            matchup.AwayConferenceWins = canonical.AwayConferenceWins;
            matchup.AwayConferenceLosses = canonical.AwayConferenceLosses;
        }
        matchup.AwayRank = canonical.AwayRank;

        // Home team
        matchup.Home = canonical.Home ?? matchup.Home;
        matchup.HomeShort = canonical.HomeShort ?? matchup.HomeShort;
        matchup.HomeShortName = canonical.HomeShortName ?? matchup.HomeShortName;
        matchup.HomeFranchiseSeasonId = canonical.HomeFranchiseSeasonId;
        matchup.HomeLogoUri = canonical.HomeLogoUri ?? matchup.HomeLogoUri;
        matchup.HomeLogoUriDark = canonical.HomeLogoUriDark;
        matchup.HomeSlug = canonical.HomeSlug ?? matchup.HomeSlug;
        matchup.HomeColor = canonical.HomeColor ?? matchup.HomeColor;
        // Same snapshot-first rule as the away side above.
        if (IsEmptyRecord(matchup.HomeWins, matchup.HomeLosses, matchup.HomeConferenceWins, matchup.HomeConferenceLosses))
        {
            matchup.HomeWins = canonical.HomeWins;
            matchup.HomeLosses = canonical.HomeLosses;
            matchup.HomeConferenceWins = canonical.HomeConferenceWins;
            matchup.HomeConferenceLosses = canonical.HomeConferenceLosses;
        }
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

        matchup.ProviderName = canonical.ProviderName;

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
    /// <param name="sport">Required for the same reason as on
    /// <see cref="ApplyCanonical"/> — the live-state mapping is
    /// sport-shaped.</param>
    public static LeagueWeekMatchupsDto.MatchupForPickDto FromCanonical(
        LeagueMatchupDto canonical,
        Sport sport)
    {
        var matchup = new LeagueWeekMatchupsDto.MatchupForPickDto
        {
            ContestId = canonical.ContestId,
            StartDateUtc = canonical.StartDateUtc,
            // Records are assigned HERE rather than in ApplyCanonical, which
            // deliberately leaves them alone. A league card's record comes
            // from the league's own PickemGroupMatchup snapshot, written when
            // the week is generated or refreshed; letting the canonical value
            // overwrite it meant the snapshot was never displayed (prod
            // 2026-09-18: Detroit at Buffalo rendered 0-0 while the row held
            // 1-0). This debug endpoint has no league context, so canonical
            // is the only source available to it.
            AwayWins = canonical.AwayWins,
            AwayLosses = canonical.AwayLosses,
            AwayConferenceWins = canonical.AwayConferenceWins,
            AwayConferenceLosses = canonical.AwayConferenceLosses,
            HomeWins = canonical.HomeWins,
            HomeLosses = canonical.HomeLosses,
            HomeConferenceWins = canonical.HomeConferenceWins,
            HomeConferenceLosses = canonical.HomeConferenceLosses,
        };
        ApplyCanonical(matchup, canonical, sport);
        return matchup;
    }
}
