using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Commands.JoinLeague;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Commands.AcceptLeagueInvitation;

public interface IAcceptLeagueInvitationCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        Guid invitationId,
        Guid userId,
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
        Guid invitationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _dbContext.PickemGroupInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);

        if (invitation is null || invitation.InviteeUserId != userId)
        {
            // Same NotFound for "doesn't exist" and "not yours" — no
            // invitation-id probing.
            return new Failure<Guid>(
                invitationId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(invitationId), "Invitation not found.")]);
        }

        if (invitation.IsRevoked || invitation.DeclinedUtc is not null)
            return new Failure<Guid>(
                invitationId,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(invitationId), "This invitation is no longer active.")]);

        if (invitation.AcceptedUtc is not null)
            return new Success<Guid>(invitation.PickemGroupId); // idempotent re-accept

        var joinResult = await _joinHandler.ExecuteAsync(
            new JoinLeagueCommand
            {
                PickemGroupId = invitation.PickemGroupId,
                UserId = userId
            },
            cancellationToken);

        // "Already a member" from the join path means the user got in some
        // other way (public browse, invite link) — the invitation's purpose
        // is fulfilled either way, so stamp and succeed. Any other failure
        // (league closed / deactivated / not found) propagates untouched.
        var alreadyMember = joinResult is Failure<Guid?> f &&
            f.Status == ResultStatus.Validation &&
            f.Errors.Any(e => e.ErrorMessage.Contains("already a member", StringComparison.OrdinalIgnoreCase));

        if (!joinResult.IsSuccess && !alreadyMember)
            return new Failure<Guid>(
                invitation.PickemGroupId,
                joinResult.Status,
                joinResult is Failure<Guid?> failure ? failure.Errors : []);

        invitation.AcceptedUtc = _dateTimeProvider.UtcNow();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} accepted invitation {InvitationId} to league {LeagueId}",
            userId, invitationId, invitation.PickemGroupId);

        return new Success<Guid>(invitation.PickemGroupId);
    }
}
