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
    private readonly IMessageDeliveryScope _deliveryScope;
    private readonly ILogger<InviteUserToLeagueCommandHandler> _logger;

    public InviteUserToLeagueCommandHandler(
        AppDataContext dbContext,
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope,
        ILogger<InviteUserToLeagueCommandHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
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

        // No DbContext write here, so bypass the MassTransit outbox and publish
        // straight to the broker. Unlike the email path, there's no prior
        // non-idempotent side effect, so a publish failure should fail the
        // request (the user simply retries) rather than be swallowed.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
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
        }

        _logger.LogInformation(
            "Published UserInvitedToPickemGroup (by username). LeagueId={LeagueId}, InviteeUserId={InviteeUserId}",
            league.Id, invitee.Id);

        return new Success<bool>(true);
    }
}
