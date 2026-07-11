using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Users
{
    /// <summary>
    /// A user changed their per-category notification opt-in flags. Published by
    /// the API (canonical owner) so the Notification service projects the new
    /// values into its local <c>UserNotificationPreferences</c> table, which its
    /// dispatch consumers read to gate sends. Carries the full flag set so the
    /// consumer is a straight upsert.
    ///
    /// <para>Idempotent projection target: republished on every change / at-least-
    /// once redelivery; the consumer upserts by UserId.</para>
    /// </summary>
    public record UserNotificationPreferencesUpdated(
        Guid UserId,
        bool PickResultEnabled,
        bool PickDeadlineReminderEnabled,
        bool ContestStartReminderEnabled,
        bool LeagueInviteEnabled,
        bool MembershipEnabled,
        bool MatchupPreviewEnabled,
        bool ScheduleChangeEnabled,
        bool OddsChangedEnabled,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport.All, null, CorrelationId, CausationId);
}
