using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Leagues.JoinLeague
{
    public interface IJoinLeagueCommandHandler
    {
        Task<Guid?> HandleAsync(JoinLeagueCommand command, CancellationToken cancellationToken = default);
    }

    public class JoinLeagueCommandHandler : IJoinLeagueCommandHandler
    {
        private readonly AppDataContext _dbContext;

        public JoinLeagueCommandHandler(AppDataContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<Guid?> HandleAsync(
            JoinLeagueCommand command,
            CancellationToken cancellationToken = default)
        {
            var membership = new PickemGroupMember()
            {
                Id = Guid.NewGuid(),
                CreatedBy = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                PickemGroupId = command.PickemGroupId,
                Role = LeagueRole.Member,
                UserId = command.UserId
            };
            await _dbContext.PickemGroupMembers.AddAsync(membership, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return membership.Id;
        }
    }
}
