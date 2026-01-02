using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Commands.JoinLeague
{
    public interface IJoinLeagueCommandHandler
    {
        Task<Result<Guid?>> ExecuteAsync(JoinLeagueCommand command, CancellationToken cancellationToken = default);
    }

    public class JoinLeagueCommandHandler : IJoinLeagueCommandHandler
    {
        private readonly ILogger<JoinLeagueCommandHandler> _logger;
        private readonly AppDataContext _dbContext;

        public JoinLeagueCommandHandler(
            ILogger<JoinLeagueCommandHandler> logger,
            AppDataContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<Result<Guid?>> ExecuteAsync(
            JoinLeagueCommand command,
            CancellationToken cancellationToken = default)
        {
            var league = await _dbContext.PickemGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == command.PickemGroupId, cancellationToken: cancellationToken);

            if (league is null)
                return new Failure<Guid?>(
                    command.PickemGroupId,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(command.PickemGroupId), "League not found")]);

            if (league.Members.Any(m => m.UserId == command.UserId))
                return new Failure<Guid?>(
                    command.PickemGroupId,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(command.UserId), "User is already a member of this league")]);

            _logger.LogInformation(
                "User {UserId} joining league {LeagueId}",
                command.UserId,
                command.PickemGroupId);

            var membership = new PickemGroupMember
            {
                Id = Guid.NewGuid(),
                CreatedBy = command.UserId,
                CreatedUtc = DateTime.UtcNow,
                PickemGroupId = command.PickemGroupId,
                Role = LeagueRole.Member,
                UserId = command.UserId
            };

            await _dbContext.PickemGroupMembers.AddAsync(membership, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "User {UserId} successfully joined league {LeagueId}",
                command.UserId,
                command.PickemGroupId);

            return new Success<Guid?>(membership.Id);
        }
    }
}
