using System;
using System.Collections.Generic;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.PickemGroups
{
    /// <summary>
    /// Per-league backfill snapshot. Members are bundled into the payload
    /// rather than published as separate per-member events — at solo-dev scale
    /// (dozens of leagues, hundreds of members total) the bundle keeps the
    /// projection insert atomic and eliminates the parent-before-child race
    /// that separate events would introduce.
    ///
    /// <para>
    /// Consumer is responsible for idempotent upsert (replace-members
    /// semantics) — repeated backfills converge on the same projection rows.
    /// </para>
    /// </summary>
    public record PickemGroupDataPublished(
        Guid GroupId,
        string Name,
        Guid CommissionerUserId,
        string PickType,
        IReadOnlyList<PickemGroupMemberSnapshot> Members,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);

    /// <summary>
    /// Embedded member row carried inside <see cref="PickemGroupDataPublished"/>.
    /// Role is the string form of <c>LeagueRole</c> (lives in API today; cross-
    /// service string is the lighter coupling than sharing the enum).
    /// </summary>
    public record PickemGroupMemberSnapshot(
        Guid UserId,
        string Role
    );
}
