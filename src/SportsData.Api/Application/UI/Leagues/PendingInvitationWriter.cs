using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Leagues;

/// <summary>
/// Owns the "one pending invitation per (league, invitee)" rule for both
/// invite paths (username picker + email-to-registered-user) so the dedupe
/// predicate and row construction can't drift between them. The predicate
/// here is the same definition of "pending" that GetPendingInvitations,
/// accept, and decline all rely on. A unique filtered index on
/// (PickemGroupId, InviteeUserId) backs this check against concurrent
/// invites — a lost race surfaces as a DbUpdateException on save.
/// </summary>
public static class PendingInvitationWriter
{
    /// <summary>
    /// Adds a pending invitation row unless one already exists. Does NOT
    /// save — callers own the SaveChanges (and its outbox flush).
    /// </summary>
    public static async Task EnsurePendingAsync(
        AppDataContext dbContext,
        Guid leagueId,
        Guid inviteeUserId,
        Guid invitedByUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var hasPending = await dbContext.PickemGroupInvitations
            .AsNoTracking()
            .AnyAsync(i =>
                i.PickemGroupId == leagueId &&
                i.InviteeUserId == inviteeUserId &&
                i.AcceptedUtc == null &&
                i.DeclinedUtc == null &&
                !i.IsRevoked,
                cancellationToken);

        if (hasPending) return;

        dbContext.PickemGroupInvitations.Add(new PickemGroupInvitation
        {
            Id = Guid.NewGuid(),
            CreatedBy = invitedByUserId,
            CreatedUtc = nowUtc,
            PickemGroupId = leagueId,
            InvitedByUserId = invitedByUserId,
            InviteeUserId = inviteeUserId
        });
    }
}
