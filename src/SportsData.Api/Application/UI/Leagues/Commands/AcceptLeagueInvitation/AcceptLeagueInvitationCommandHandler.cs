using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Commands.JoinLeague;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Commands.AcceptLeagueInvitation;

public interface IAcceptLeagueInvitationCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        AcceptLeagueInvitationCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Accepts a pending league invitation. Membership is created by delegating
/// to <see cref="IJoinLeagueCommandHandler"/> so every join gate (deactivated
/// league, join-policy expiry, first-game fallback) applies identically to
/// invited users — an invitation is a pointer to the league, not a bypass of
/// its rules. On success the invitation is stamped AcceptedUtc. If the user
/// already became a member through another path, the stamp self-heals.
/// </summary>
public class AcceptLeagueInvitationCommandHandler : IAcceptLeagueInvitationCommandHandler
{
    private readonly AppDataContext _dbContext;
    private readonly IJoinLeagueCommandHandler _joinHandler;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AcceptLeagueInvitationCommandHandler> _logger;

    public AcceptLeagueInvitationCommandHandler(
        AppDataContext dbContext,
        IJoinLeagueCommandHandler joinHandler,
        IDateTimeProvider dateTimeProvider,
        ILogger<AcceptLeagueInvitationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _joinHandler = joinHandler;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        AcceptLeagueInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _dbContext.PickemGroupInvitations
            .FirstOrDefaultAsync(i => i.Id == command.InvitationId, cancellationToken);

        if (invitation is null || invitation.InviteeUserId != command.UserId)
        {
            // Same NotFound for "doesn't exist" and "not yours" — no
            // invitation-id probing.
            return new Failure<Guid>(
                command.InvitationId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.InvitationId), "Invitation not found.")]);
        }

        if (invitation.IsRevoked || invitation.DeclinedUtc is not null)
            return new Failure<Guid>(
                command.InvitationId,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.InvitationId), "This invitation is no longer active.")]);

        if (invitation.AcceptedUtc is not null)
            return new Success<Guid>(invitation.PickemGroupId); // idempotent re-accept

        var joinResult = await _joinHandler.ExecuteAsync(
            new JoinLeagueCommand
            {
                PickemGroupId = invitation.PickemGroupId,
                UserId = command.UserId
            },
            cancellationToken);

        // "Already a member" from the join path means the user got in some
        // other way (public browse, invite link) — the invitation's purpose
        // is fulfilled either way, so stamp and succeed. Matched on the
        // STABLE ErrorCode, not message text. Any other failure (league
        // closed / deactivated / not found) propagates untouched.
        var alreadyMember = joinResult is Failure<Guid?> f &&
            f.Errors.Any(e => e.ErrorCode == JoinLeagueErrorCodes.AlreadyMember);

        if (!joinResult.IsSuccess && !alreadyMember)
            return new Failure<Guid>(
                invitation.PickemGroupId,
                joinResult.Status,
                joinResult is Failure<Guid?> failure ? failure.Errors : []);

        invitation.AcceptedUtc = _dateTimeProvider.UtcNow();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} accepted invitation {InvitationId} to league {LeagueId}",
            command.UserId, command.InvitationId, invitation.PickemGroupId);

        return new Success<Guid>(invitation.PickemGroupId);
    }
}
