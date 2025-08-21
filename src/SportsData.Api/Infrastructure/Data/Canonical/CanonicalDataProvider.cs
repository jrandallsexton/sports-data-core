using Dapper;

using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

using System.Data;
using SportsData.Api.Application.UI.Leagues.Dtos;

namespace SportsData.Api.Infrastructure.Data.Canonical
{
    public interface IProvideCanonicalData
    {
        Task<TeamCardDto?> ExecuteAsync(
            GetTeamCardQuery query, CancellationToken cancellationToken = default);

        Task<Dictionary<string, Guid>> GetFranchiseIdsBySlugsAsync(Sport sport, List<string> slugs);

        Task<Dictionary<string, Guid>> GetConferenceIdsBySlugsAsync(Sport sport, List<string> slugs);

        Task<SeasonWeek?> GetCurrentSeasonWeek();

        Task<List<Matchup>> GetMatchupsForCurrentWeek();

        Task<List<LeagueWeekMatchupsDto.MatchupForPickDto>> GetMatchupsByContestIds(List<Guid> contestIds);

        Task<MatchupForPreviewDto> GetMatchupForPreview(Guid contestId);
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
            var cardSqlPath =
                Path.Combine("C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Data\\Canonical\\Sql\\",
                    "GetTeamCard.sql");
            var seasonsSqlPath =
                Path.Combine("C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Data\\Canonical\\Sql\\",
                    "GetTeamSeasons.sql");
            var scheduleSqlPath =
                Path.Combine("C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Data\\Canonical\\Sql\\",
                    "GetTeamCardSchedule.sql");

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

        public async Task<SeasonWeek?> GetCurrentSeasonWeek()
        {
            const string sql = @"
        SELECT sw.""Id"" AS ""Id"",
               sw.""Number"" AS ""WeekNumber"",
               s.""Id"" AS ""SeasonId"",
               s.""Year"" AS ""SeasonYear""
        FROM public.""Season"" s
        JOIN public.""SeasonWeek"" sw ON sw.""SeasonId"" = s.""Id""
        JOIN public.""SeasonPhase"" sp ON sp.""Id"" = sw.""SeasonPhaseId""
        WHERE sp.""Name"" = 'Regular Season'
          AND sw.""StartDate"" >= CURRENT_DATE
        ORDER BY sw.""StartDate""
        LIMIT 1;";

            try
            {
                var result = await _connection.QueryFirstOrDefaultAsync<SeasonWeek>(sql);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve current season week.");
                return null;
            }
        }

        public async Task<List<Matchup>> GetMatchupsForCurrentWeek()
        {
            var sqlPath = Path.Combine(
                "C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Data\\Canonical\\Sql\\",
                "GetMatchupsForCurrentWeek.sql");

            var sql = await File.ReadAllTextAsync(sqlPath);

            try
            {
                var results = await _connection.QueryAsync<Matchup>(
                    sql);

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch current week matchups");

                throw;
            }
        }

        public async Task<List<LeagueWeekMatchupsDto.MatchupForPickDto>> GetMatchupsByContestIds(List<Guid> contestIds)
        {
            var sqlPath = Path.Combine(
                "C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Data\\Canonical\\Sql\\",
                "GetLeagueMatchupsByContestIds.sql");

            var sql = await File.ReadAllTextAsync(sqlPath);

            var results = await _connection.QueryAsync<LeagueWeekMatchupsDto.MatchupForPickDto>(
                sql,
                new { ContestIds = contestIds }, // contestIds = List<Guid>
                commandType: CommandType.Text
            );

            return results.ToList();
        }

        public async Task<MatchupForPreviewDto> GetMatchupForPreview(Guid contestId)
        {
            var sqlPath = Path.Combine(
                "C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Data\\Canonical\\Sql\\",
                "GetMatchupForPreviewGeneration.sql");

            var sql = await File.ReadAllTextAsync(sqlPath);

            var result = await _connection.QuerySingleOrDefaultAsync<MatchupForPreviewDto>(
                sql,
                new { ContestId = contestId },
                commandType: CommandType.Text
            );

            return result ?? throw new Exception("Not found");
        }

    }
}
