using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries;

using System.Data;
using Dapper;
using SportsData.Core.Common;

namespace SportsData.Api.Infrastructure.Data.Canonical
{
    public interface IProvideCanonicalData
    {
        Task<TeamCardDto?> ExecuteAsync(
            GetTeamCardQuery query, CancellationToken cancellationToken = default);

        Task<Dictionary<string, Guid>> GetFranchiseIdsBySlugsAsync(Sport sport, List<string> slugs);

        Task<Dictionary<string, Guid>> GetConferenceIdsBySlugsAsync(Sport sport, List<string> slugs);
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
            var seasonsSqlPath = Path.Combine("C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Data\\Canonical\\Sql\\", "GetTeamSeasons.sql");
            var scheduleSqlPath = Path.Combine("C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Data\\Canonical\\Sql\\", "GetTeamCardSchedule.sql");

            var cardSql = await File.ReadAllTextAsync(cardSqlPath, cancellationToken);
            var seasonsSql = await File.ReadAllTextAsync(seasonsSqlPath, cancellationToken);
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

                var seasons = (await _connection.QueryAsync<int>(seasonsSql, new { Slug = query.Slug })).ToList();
                teamCard.SeasonYears = seasons;

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

        public async Task<Dictionary<string, Guid>> GetFranchiseIdsBySlugsAsync(
            Sport sport,
            List<string> slugs)
        {
            const string sql =
                "SELECT \"Slug\", \"Id\" " +
                "FROM public.\"Franchise\" " +
                "WHERE \"Sport\" = @Sport AND \"Slug\" IN @Slugs;";

            try
            {
                var results = await _connection.QueryAsync<(string Slug, Guid Id)>(
                    sql,
                    new { Sport = (int)sport, Slugs = slugs }
                );

                return results.ToDictionary(x => x.Slug, x => x.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve Franchise IDs for slugs: {@Slugs}", slugs);
                return new Dictionary<string, Guid>();
            }
        }

        public async Task<Dictionary<string, Guid>> GetConferenceIdsBySlugsAsync(
            Sport sport,
            List<string> slugs)
        {
            const string sql =
                "SELECT \"Slug\", \"Id\" " +
                "FROM public.\"Group\" " +
                "WHERE \"Slug\" = ANY(@Slugs);";

            try
            {
                var results = await _connection.QueryAsync<(string Slug, Guid Id)>(
                    sql,
                    new { Sport = (int)sport, Slugs = slugs }
                );

                return results.ToDictionary(x => x.Slug, x => x.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve Franchise IDs for slugs: {@Slugs}", slugs);
                return new Dictionary<string, Guid>();
            }
        }
    }
}
