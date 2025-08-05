using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries;

using System.Data;
using Dapper;

namespace SportsData.Api.Infrastructure.Data.Canonical
{
    public interface IProvideCanonicalData
    {
        Task<TeamCardDto?> ExecuteAsync(
            GetTeamCardQuery query, CancellationToken cancellationToken = default);
    }

    public class CanonicalDataProvider : IProvideCanonicalData
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<CanonicalDataProvider> _logger;

        public CanonicalDataProvider(
            IDbConnection connection,
            ILogger<CanonicalDataProvider> logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public async Task<TeamCardDto?> ExecuteAsync(
            GetTeamCardQuery query,
            CancellationToken cancellationToken = default)
        {
            var cardSqlPath = Path.Combine("C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Data\\Canonical\\Sql\\", "GetTeamCard.sql");
            var scheduleSqlPath = Path.Combine("C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Data\\Canonical\\Sql\\", "GetTeamCardSchedule.sql");

            var cardSql = await File.ReadAllTextAsync(cardSqlPath, cancellationToken);
            var scheduleSql = await File.ReadAllTextAsync(scheduleSqlPath, cancellationToken);

            var parameters = new
            {
                Slug = query.Slug,
                SeasonYear = query.SeasonYear
            };

            try
            {
                var teamCard = await _connection.QueryFirstOrDefaultAsync<TeamCardDto>(
                    cardSql,
                    parameters);

                if (teamCard is null)
                    return null;

                var schedule = (await _connection.QueryAsync<TeamCardScheduleItemDto>(
                    scheduleSql,
                    parameters)).ToList();

                teamCard.Schedule = schedule;

                return teamCard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load TeamCard or Schedule for {@Query}", query);
                return null;
            }
        }

    }
}
