using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Picks
{
    /// <summary>
    /// A user's pick was scored after the underlying contest finalized.
    /// This is the headline event for the Notification service (design doc
    /// <c>docs/architecture/notification-service-events-and-state.md</c> §4 / §5
    /// v1) — every member who picked a finalized contest gets one of these
    /// and it drives the "#1 pick result" notification.
    ///
    /// <para>
    /// Fat-event by design: Notification doesn't project User, Contest, or
    /// League, so the publisher carries the <b>structured display facts</b> and
    /// the Notification consumer composes the copy. <c>PickScoringProcessor</c>
    /// populates the team abbreviations, <see cref="PickedIsHome"/>, and
    /// <see cref="PickedSpread"/> (via the FranchiseSeason join on the matchup
    /// result); the full team-name fields and <see cref="DisplayName"/> remain
    /// <c>null</c> (unused by current copy). The publisher deliberately sends no
    /// pre-composed strings — formatting lives in the consumer. Consumers must
    /// degrade gracefully on the nullable fields.
    /// </para>
    /// </summary>
    public record UserPickScored(
        Guid UserId,
        string? DisplayName,
        Guid ContestId,
        string? AwayName,
        string? HomeName,
        string? AwayAbbreviation,
        string? HomeAbbreviation,
        int AwayScore,
        int HomeScore,
        bool? IsCorrect,
        // Which side the user picked, resolved publisher-side (UserPick stores
        // FranchiseId; the matchup result carries the per-side FranchiseId).
        // Null for Over/Under picks or when it can't be resolved — the consumer
        // falls back to the generic copy.
        bool? PickedIsHome,
        // The picked side's spread as a signed number (home = matchup spread,
        // away = its negation), the same value scoring ran on. Null for straight-
        // up picks (or ATS with no/zero spread). The consumer formats it.
        double? PickedSpread,
        Guid LeagueId,
        string LeagueName,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
