using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.PickemGroups
{
    /// <summary>
    /// A registered user was invited to a league. Published by the API when an
    /// invite (today: the email path) resolves to an existing user who is not
    /// already a member, so the Notification service can push an invite with a
    /// deep-link to the league-invite preview.
    ///
    /// <para>
    /// <c>LeagueName</c> rides on the event so the consumer needs no PickemGroup
    /// projection read (and dodges the race of that projection not having landed
    /// yet). Inviting by username (autocomplete) will publish this same event.
    /// </para>
    /// </summary>
    public record UserInvitedToPickemGroup(
        Guid InviteeUserId,
        Guid GroupId,
        string LeagueName,
        Guid InvitedByUserId,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
