using SportsData.Api.Infrastructure.Data;

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

        public async Task<Guid?> HandleAsync(JoinLeagueCommand command, CancellationToken cancellationToken = default)
        {
            await Task.Delay(100);
            throw new NotImplementedException();
        }
    }
}
