using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.PickemGroups
{
    /// <summary>
    /// A league was created. Carries the league-level fields the Notification
    /// projection needs so a new league is queryable immediately, without
    /// waiting for an operator backfill (<see cref="PickemGroupsRequested"/>).
    ///
    /// <para>
    /// <c>PickType</c> is the string form of API's <c>PickType</c> enum
    /// (StraightUp / AgainstTheSpread / OverUnder) — same lighter-coupling
    /// convention as <see cref="PickemGroupMemberSnapshot.Role"/>: a cross-
    /// service string beats sharing the enum. Notification reads it to decide
    /// whether a line move is relevant to a league's pickers (straight-up
    /// leagues don't care about odds).
    /// </para>
    /// </summary>
    public record PickemGroupCreated(
        Guid GroupId,
        string Name,
        Guid CommissionerUserId,
        string PickType,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
