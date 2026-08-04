using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Commands.SendLeagueInvite;

public interface ISendLeagueInviteCommandHandler
{
    Task<Result<bool>> ExecuteAsync(SendLeagueInviteCommand command, CancellationToken cancellationToken = default);
}

public class SendLeagueInviteCommandHandler : ISendLeagueInviteCommandHandler
{
    private readonly AppDataContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly NotificationConfig _notificationConfig;
    private readonly IEventBus _eventBus;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<SendLeagueInviteCommandHandler> _logger;

    public SendLeagueInviteCommandHandler(
        AppDataContext dbContext,
        INotificationService notificationService,
        IOptions<NotificationConfig> notificationConfig,
        IEventBus eventBus,
        IDateTimeProvider dateTimeProvider,
        ILogger<SendLeagueInviteCommandHandler> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _notificationConfig = notificationConfig.Value;
        _eventBus = eventBus;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<bool>> ExecuteAsync(
        SendLeagueInviteCommand command,
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
        // before the email is sent so a non-member can't blast invites into a
        // league they don't belong to. Mirrors InviteUserToLeagueCommandHandler.
        var inviterIsMember = await _dbContext.PickemGroupMembers
            .AsNoTracking()
            .AnyAsync(m => m.PickemGroupId == league.Id && m.UserId == command.InvitedByUserId, cancellationToken);

        if (!inviterIsMember)
            return new Failure<bool>(
                false,
                ResultStatus.Forbid,
                [new ValidationFailure(nameof(command.InvitedByUserId), "Only league members can invite others.")]);

        // TODO: Dynamically set the domain based on environment
        var inviteUrl = $"https://{_notificationConfig.Email.UrlBase}/app/join/{league.Id.ToString().Replace("-", string.Empty)}";

        await _notificationService.SendEmailAsync(
            command.Email,
            _notificationConfig.Email.TemplateIdInvitation,
            new
            {
                firstName = command.InviteeName ?? "friend",
                leagueName = league.Name,
                joinUrl = inviteUrl
            });

        // If the invited email belongs to a registered user who isn't already a
        // member, also announce the invite so the Notification service can push
        // a deep-link to the league-invite preview. Unregistered email →
        // email-only, unchanged.
        var invitee = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

        if (invitee is not null)
        {
            var alreadyMember = await _dbContext.PickemGroupMembers
                .AsNoTracking()
                .AnyAsync(m => m.PickemGroupId == league.Id && m.UserId == invitee.Id, cancellationToken);

            if (!alreadyMember)
            {
                // Best-effort: the invite email has already been sent (and is
                // non-idempotent — a retry re-sends it), so a DB/broker outage
                // must not fail the request. Swallow + log; losing the push and
                // the home-card row is acceptable, re-emailing on retry is not.
                //
                // The invitation row powers the "Pending Invitations" home
                // card. With a DbContext write in play, the publish now rides
                // the bus outbox and commits with the row on SaveChanges
                // (previously DeliveryMode.Direct with nothing to save).
                try
                {
                    var hasPending = await _dbContext.PickemGroupInvitations
                        .AsNoTracking()
                        .AnyAsync(i =>
                            i.PickemGroupId == league.Id &&
                            i.InviteeUserId == invitee.Id &&
                            i.AcceptedUtc == null &&
                            i.DeclinedUtc == null &&
                            !i.IsRevoked,
                            cancellationToken);

                    if (!hasPending)
                    {
                        await _dbContext.PickemGroupInvitations.AddAsync(new PickemGroupInvitation
                        {
                            Id = Guid.NewGuid(),
                            CreatedBy = command.InvitedByUserId,
                            CreatedUtc = _dateTimeProvider.UtcNow(),
                            PickemGroupId = league.Id,
                            InvitedByUserId = command.InvitedByUserId,
                            InviteeUserId = invitee.Id
                        }, cancellationToken);
                    }

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

                    await _dbContext.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Persisted invitation + published UserInvitedToPickemGroup for registered invitee. LeagueId={LeagueId}, InviteeUserId={InviteeUserId}",
                        league.Id, invitee.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to persist/publish invite; invite email already sent so continuing. LeagueId={LeagueId}, InviteeUserId={InviteeUserId}",
                        league.Id, invitee.Id);
                }
            }
        }

        return new Success<bool>(true);
    }
}
