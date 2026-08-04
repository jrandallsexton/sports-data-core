using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Commands.DeclineLeagueInvitation;

public interface IDeclineLeagueInvitationCommandHandler
{
    Task<Result<bool>> ExecuteAsync(
        Guid invitationId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Declines a pending league invitation — stamps DeclinedUtc so it drops off
/// the "Pending Invitations" home card. No membership side effects, no
/// events; a member can re-invite later (a fresh row is created).
/// </summary>
public class DeclineLeagueInvitationCommandHandler : IDeclineLeagueInvitationCommandHandler
{
    private readonly AppDataContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<DeclineLeagueInvitationCommandHandler> _logger;

    public DeclineLeagueInvitationCommandHandler(
        AppDataContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<DeclineLeagueInvitationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<bool>> ExecuteAsync(
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
            return new Failure<bool>(
                false,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(invitationId), "Invitation not found.")]);
        }

        if (invitation.AcceptedUtc is not null)
            return new Failure<bool>(
                false,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(invitationId), "This invitation was already accepted.")]);

        if (invitation.DeclinedUtc is not null)
            return new Success<bool>(true); // idempotent re-decline

        invitation.DeclinedUtc = _dateTimeProvider.UtcNow();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} declined invitation {InvitationId} to league {LeagueId}",
            userId, invitationId, invitation.PickemGroupId);

        return new Success<bool>(true);
    }
}
