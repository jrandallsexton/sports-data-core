using System.Data;

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
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.LeagueId), $"League with ID {command.LeagueId} not found.")]);

        if (league.CommissionerUserId != command.UserId)
            return new Failure<Guid>(
                default!,
                ResultStatus.Unauthorized,
                [new ValidationFailure(nameof(command.UserId), $"User {command.UserId} is not the commissioner of league {command.LeagueId}.")]);

        // Don't let a commissioner nuke a league whose picks have been SCORED —
        // that is real history. Leagues with only unscored picks are fair game:
        // parameters are immutable, so starting over is the supported way to
        // fix a misconfigured league, and unscored picks are cheap to re-enter.
        //
        // Serializable transaction: the has-scored-picks check and the cascade
        // delete run in one unit so a result written between the two operations
        // (the scoring consumer grading picks mid-delete) can't sneak through.
        // FK constraints alone won't block the race.
        //
        // Wrap in the DbContext execution strategy: EnableRetryOnFailure is configured
        // globally for Npgsql (see Core/DependencyInjection/ServiceRegistration.cs),
        // and raw BeginTransactionAsync under a retry strategy throws at runtime unless
        // the transaction body is scoped inside strategy.ExecuteAsync so the whole unit
        // can retry atomically on serialization failures (SQLSTATE 40001).
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<Result<Guid>>(async () =>
        {
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            // The deletion rule (owner decision 2026-09-03): allowed until
            // SCORED human picks exist. League parameters are immutable, so
            // "made a few picks, want a different league" must be able to
            // start over — unscored picks are intent, cheaply re-entered in
            // the replacement league; scored picks are HISTORY, and history
            // is what this guard protects. Synthetic (StatBot) picks never
            // count, scored or not — the bot joins and picks on its own
            // schedule, and letting it close the deletion window would lock
            // every league without any human investment.
            var hasScoredPicks = await _dbContext.UserPicks
                .AnyAsync(
                    p => p.PickemGroupId == command.LeagueId
                         && !_dbContext.Users.Any(u => u.Id == p.UserId && u.IsSynthetic)
                         && _dbContext.PickResults.Any(r => r.UserPickId == p.Id),
                    cancellationToken);

            if (hasScoredPicks)
                return new Failure<Guid>(
                    default!,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(command.LeagueId), "Cannot delete a league that already has scored picks.")]);

            _logger.LogInformation(
                "Deleting league {LeagueId} by commissioner {UserId}",
                command.LeagueId,
                command.UserId);

            // Remove all members
            _dbContext.PickemGroupMembers.RemoveRange(league.Members);

            // Remove pick results FIRST — PickResult has no FK to UserPick
            // (loose coupling, unique index only), so nothing cascades and
            // skipping this would orphan result rows for the synthetic picks
            // that scoring may already have graded.
            _dbContext.PickResults.RemoveRange(
                _dbContext.PickResults.Where(r =>
                    _dbContext.UserPicks.Any(p =>
                        p.Id == r.UserPickId && p.PickemGroupId == command.LeagueId)));

            // Remove all picks
            _dbContext.UserPicks.RemoveRange(
                _dbContext.UserPicks.Where(p => p.PickemGroupId == command.LeagueId));

            // Remove all matchups
            _dbContext.PickemGroupMatchups.RemoveRange(
                _dbContext.PickemGroupMatchups.Where(m => m.GroupId == command.LeagueId));

            // Remove the league itself
            _dbContext.PickemGroups.Remove(league);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully deleted league {LeagueId}", command.LeagueId);

            return new Success<Guid>(command.LeagueId);
        });
    }
}
