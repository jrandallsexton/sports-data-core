using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;

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

    public SendLeagueInviteCommandHandler(
        AppDataContext dbContext,
        INotificationService notificationService,
        IOptions<NotificationConfig> notificationConfig)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _notificationConfig = notificationConfig.Value;
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

        // TODO: Dynamically set the domain based on environment
        var inviteUrl = $"https://dev.sportdeets.com/app/join/{league.Id.ToString().Replace("-", string.Empty)}";

        await _notificationService.SendEmailAsync(
            command.Email,
            _notificationConfig.Email.TemplateIdInvitation,
            new
            {
                firstName = command.InviteeName ?? "friend",
                leagueName = league.Name,
                joinUrl = inviteUrl
            });

        return new Success<bool>(true);
    }
}
