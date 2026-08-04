using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.UI.Leagues.Commands.InviteUserToLeague;

public interface IInviteUserToLeagueCommandHandler
{
    Task<Result<bool>> ExecuteAsync(InviteUserToLeagueCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Invite an already-registered user (picked from the username autocomplete) to
/// a league. Publishes <see cref="UserInvitedToPickemGroup"/> so the Notification
/// service pushes the deep-link to the league-invite preview — the same path PR1
/// wired for the email-match case. No email is sent here: the invitee is
/// registered, so the push (and in-app) channel is the right one.
/// </summary>
public class InviteUserToLeagueCommandHandler : IInviteUserToLeagueCommandHandler
{
    private readonly AppDataContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<InviteUserToLeagueCommandHandler> _logger;

    public InviteUserToLeagueCommandHandler(
        AppDataContext dbContext,
        IEventBus eventBus,
        IDateTimeProvider dateTimeProvider,
        ILogger<InviteUserToLeagueCommandHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<bool>> ExecuteAsync(
        InviteUserToLeagueCommand command,
        CancellationToken cancellationToken = default)
    {
        var league = await _dbContext.PickemGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.LeagueId, cancellationToken);

        if (league is null)
            return new Failure<bool>(
                false,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.LeagueId), $"League with ID {command.LeagueId} not found.")]);

        // Authorization: only a member of the league may invite others. Checked
        // before any further work so a non-member can't probe membership or
        // trigger a notification.
        var inviterIsMember = await _dbContext.PickemGroupMembers
            .AsNoTracking()
            .AnyAsync(m => m.PickemGroupId == league.Id && m.UserId == command.InvitedByUserId, cancellationToken);

        if (!inviterIsMember)
            return new Failure<bool>(
                false,
                ResultStatus.Forbid,
                [new ValidationFailure(nameof(command.InvitedByUserId), "Only league members can invite others.")]);

        var invitee = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == command.InviteeUserId, cancellationToken);

        if (invitee is null)
            return new Failure<bool>(
                false,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.InviteeUserId), $"User with ID {command.InviteeUserId} not found.")]);

        var alreadyMember = await _dbContext.PickemGroupMembers
            .AsNoTracking()
            .AnyAsync(m => m.PickemGroupId == league.Id && m.UserId == invitee.Id, cancellationToken);

        if (alreadyMember)
            return new Failure<bool>(
                false,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.InviteeUserId), "That user is already a member of this league.")]);

        // Persist the invitation so it appears on the invitee's home page
        // ("Pending Invitations" card) — the push notification alone is
        // ephemeral. Dedupe: an existing pending row for this group+invitee
        // is refreshed-by-no-op (re-inviting doesn't stack rows); a
        // previously declined/revoked invite gets a fresh row so the league
        // reappears on their home. Shared with SendLeagueInvite so the two
        // invite paths can't drift.
        await PendingInvitationWriter.EnsurePendingAsync(
            _dbContext,
            league.Id,
            invitee.Id,
            command.InvitedByUserId,
            _dateTimeProvider.UtcNow(),
            cancellationToken);

        // Publish BEFORE SaveChanges so the bus-outbox interceptor commits the
        // event together with the invitation row (this handler previously had
        // no write and used DeliveryMode.Direct; the write changes that).
        await _eventBus.Publish(
            new UserInvitedToPickemGroup(
                InviteeUserId: invitee.Id,
                GroupId: league.Id,
                LeagueName: league.Name,
                InvitedByUserId: command.InvitedByUserId,
                Sport: league.Sport,
                SeasonYear: null,
                CorrelationId: Guid.NewGuid(),
                CausationId: Guid.NewGuid()),
            cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsPendingInvitationDuplicate(ex))
        {
            // Lost a race with a concurrent invite for the same
            // (league, invitee): the unique filtered index rejected our row.
            // The pending invitation exists and the winning request's outbox
            // carried the notification event, so this is idempotent success —
            // exactly what the pre-check would have concluded a moment later.
            _logger.LogInformation(
                "Concurrent invite race lost (pending row already exists). LeagueId={LeagueId}, InviteeUserId={InviteeUserId}",
                league.Id, invitee.Id);
            return new Success<bool>(true);
        }

        _logger.LogInformation(
            "Published UserInvitedToPickemGroup (by username). LeagueId={LeagueId}, InviteeUserId={InviteeUserId}",
            league.Id, invitee.Id);

        return new Success<bool>(true);
    }

    /// <summary>
    /// True only for a unique-violation on the pending-invitation filtered
    /// index (IX_PickemGroupInvitations_PickemGroupId_InviteeUserId). Any
    /// other database failure re-throws via the exception filter.
    /// </summary>
    private static bool IsPendingInvitationDuplicate(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_PickemGroupInvitations_PickemGroupId_InviteeUserId"
        };
}
