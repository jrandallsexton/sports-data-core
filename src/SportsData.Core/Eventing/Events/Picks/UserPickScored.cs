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
    /// League, so the publisher carries the display facts that compose the
    /// notification copy. Today's publisher (<c>PickScoringProcessor</c>)
    /// populates the cheap fields; the team-name fields and
    /// <see cref="DisplayName"/> are <c>null</c> pending fat-payload joins.
    /// Consumers must degrade gracefully on the nullable fields.
    /// </para>
    /// </summary>
    public record UserPickScored(
        Guid UserId,
        string? DisplayName,
        Guid ContestId,
        string? AwayName,
        string? HomeName,
        int AwayScore,
        int HomeScore,
        string? PickValue,
        bool? IsCorrect,
        Guid LeagueId,
        string LeagueName,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
