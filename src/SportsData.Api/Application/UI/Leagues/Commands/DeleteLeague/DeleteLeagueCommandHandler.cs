using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Commands.DeleteLeague;

public interface IDeleteLeagueCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(DeleteLeagueCommand command, CancellationToken cancellationToken = default);
}

public class DeleteLeagueCommandHandler : IDeleteLeagueCommandHandler
{
    private readonly ILogger<DeleteLeagueCommandHandler> _logger;
    private readonly AppDataContext _dbContext;

    public DeleteLeagueCommandHandler(
        ILogger<DeleteLeagueCommandHandler> logger,
        AppDataContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        DeleteLeagueCommand command,
        CancellationToken cancellationToken = default)
    {
        var league = await _dbContext.PickemGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == command.LeagueId, cancellationToken: cancellationToken);

        if (league is null)
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.LeagueId), $"League with ID {command.LeagueId} not found.")]);

        if (league.CommissionerUserId != command.UserId)
            return new Failure<Guid>(
                default,
                ResultStatus.Unauthorized,
                [new ValidationFailure(nameof(command.UserId), $"User {command.UserId} is not the commissioner of league {command.LeagueId}.")]);

        _logger.LogInformation(
            "Deleting league {LeagueId} by commissioner {UserId}",
            command.LeagueId,
            command.UserId);

        // Remove all members
        _dbContext.PickemGroupMembers.RemoveRange(league.Members);

        // Remove all picks
        _dbContext.UserPicks.RemoveRange(
            _dbContext.UserPicks.Where(p => p.PickemGroupId == command.LeagueId));

        // Remove all matchups
        _dbContext.PickemGroupMatchups.RemoveRange(
            _dbContext.PickemGroupMatchups.Where(m => m.GroupId == command.LeagueId));

        // Remove the league itself
        _dbContext.PickemGroups.Remove(league);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully deleted league {LeagueId}", command.LeagueId);

        return new Success<Guid>(command.LeagueId);
    }
}
