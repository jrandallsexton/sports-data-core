using Dapper;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamCard;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

using System.Data;
using static SportsData.Api.Application.UI.Rankings.Dtos.RankingsByPollIdByWeekDto;

namespace SportsData.Api.Infrastructure.Data.Canonical
{
    public class CanonicalDataProvider : IProvideCanonicalData
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<CanonicalDataProvider> _logger;
        private readonly CanonicalDataQueryProvider _queryProvider;

        public CanonicalDataProvider(
            ILogger<CanonicalDataProvider> logger,
            IDbConnection connection,
            CanonicalDataQueryProvider queryProvider)
        {
            _logger = logger;
            _connection = connection;
            _queryProvider = queryProvider;
        }

        public async Task<TeamCardDto?> GetTeamCard(
            GetTeamCardQuery query,
            CancellationToken cancellationToken = default)
        {

            var cardSql = _queryProvider.GetTeamCard();
            var seasonsSql = _queryProvider.GetTeamSeasons();
            var scheduleSql = _queryProvider.GetTeamCardSchedule();

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
                var schedule = (await _connection.QueryAsync<TeamCardScheduleItemDto>(
                    scheduleSql,
                    parameters)).ToList();

                return teamCard with 
                { 
                    SeasonYears = seasons,
                    Schedule = schedule
                };
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

        public async Task<Dictionary<Guid, string>> GetConferenceIdsBySlugsAsync(
            Sport sport,
            int seasonYear,
            List<string> slugs)
        {
            const string sql =
                "SELECT \"Id\", \"Slug\" " +
                "FROM public.\"GroupSeason\" " +
                "WHERE \"Slug\" = ANY(@Slugs) AND \"SeasonYear\" = @SeasonYear;";

            try
            {
                var results = await _connection.QueryAsync<(Guid Id, string Slug) >(
                    sql,
                    new { Sport = (int)sport, Slugs = slugs, SeasonYear = seasonYear }
                );

                return results.ToDictionary(x => x.Id, x => x.Slug );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve Franchise IDs for slugs: {@Slugs}", slugs);
                return new Dictionary<Guid, string>();
            }
        }

        public async Task<List<ConferenceDivisionNameAndSlugDto>> GetConferenceNamesAndSlugsForSeasonYear(int seasonYear)
        {
            const string sql = @"
        SELECT DISTINCT 
            gsParent.""Name"" as ""Division"", 
            gs.""ShortName"", 
            gs.""Slug"" 
        FROM public.""GroupSeason"" gs
        INNER JOIN public.""GroupSeason"" gsParent 
            ON gsParent.""Id"" = gs.""ParentId""
        WHERE gs.""IsConference"" = true 
          AND gs.""SeasonYear"" = @SeasonYear
        ORDER BY gs.""ShortName"";";

            try
            {
                var results = await _connection.QueryAsync<ConferenceDivisionNameAndSlugDto>(
                    sql,
                    new { SeasonYear = seasonYear }
                );

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve conference divisions, names, and slugs for SeasonYear: {SeasonYear}", seasonYear);
                return [];
            }
        }
        
        public async Task<List<FranchiseSeasonCompetitionResultDto>> GetFranchiseSeasonCompetitionResultsByFranchiseSeasonId(Guid franchiseSeasonId)
        {
            var sql = _queryProvider.GetFranchiseSeasonCompetitionResultsByFranchiseSeasonId();

            var results = await _connection.QueryAsync<FranchiseSeasonCompetitionResultDto>(
                sql,
                new { FranchiseSeasonId = franchiseSeasonId },
                commandType: CommandType.Text
            );

            return results.ToList();
        }

        public async Task<List<Matchup>> GetMatchupsForCurrentWeek()
        {
            var sql = _queryProvider.GetMatchupsForCurrentWeek();

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

        public async Task<List<Matchup>> GetMatchupsForSeasonWeek(int seasonYear, int seasonWeekNumber)
        {
            var sql = _queryProvider.GetMatchupsForSeasonWeek();

            try
            {
                var results = await _connection.QueryAsync<Matchup>(
                    sql,
                    new { SeasonYear = seasonYear, SeasonWeekNumber = seasonWeekNumber });

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
            _logger.LogInformation(
                "CanonicalDataProvider.GetMatchupsByContestIds called with {ContestCount} contestIds", 
                contestIds?.Count ?? 0);
            
            try
            {
                if (contestIds == null || contestIds.Count == 0)
                {
                    _logger.LogWarning("GetMatchupsByContestIds called with null or empty contestIds list");
                    return [];
                }

                var sql = _queryProvider.GetLeagueMatchupsByContestIds();
                
                _logger.LogDebug(
                    "Executing Dapper query for {ContestCount} contests", 
                    contestIds.Count);

                var results = await _connection.QueryAsync<LeagueWeekMatchupsDto.MatchupForPickDto>(
                    sql,
                    new { ContestIds = contestIds }, // contestIds = List<Guid>
                    commandType: CommandType.Text
                );

                var resultList = results.ToList();
                
                _logger.LogInformation(
                    "Retrieved {ResultCount} matchups from database for {ContestCount} contestIds", 
                    resultList.Count, 
                    contestIds.Count);

                return resultList;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Error in GetMatchupsByContestIds for {ContestCount} contests", 
                    contestIds?.Count ?? 0);
                throw;
            }
        }

        public async Task<MatchupForPreviewDto> GetMatchupForPreview(Guid contestId)
        {
            var sql = _queryProvider.GetMatchupForPreviewGeneration();

            var result = await _connection.QuerySingleOrDefaultAsync<MatchupForPreviewDto>(
                sql,
                new { ContestId = contestId },
                commandType: CommandType.Text
            );

            return result ?? throw new Exception("Not found");
        }

        public async Task<Dictionary<Guid, MatchupForPreviewDto>> GetMatchupsForPreview(IReadOnlyCollection<Guid> contestIds, CancellationToken cancellationToken = default)
        {
            if (contestIds.Count == 0)
                return new Dictionary<Guid, MatchupForPreviewDto>();

            var sql = _queryProvider.GetMatchupsForPreviewGeneration_Batch();

            var results = await _connection.QueryAsync<MatchupForPreviewDto>(
                new CommandDefinition(
                    sql,
                    new { ContestIds = contestIds.ToArray() },
                    commandType: CommandType.Text,
                    cancellationToken: cancellationToken
                )
            );

            return results.ToDictionary(m => m.ContestId);
        }

        public async Task<MatchupResult> GetMatchupResult(Guid contestId)
        {
            var sql = _queryProvider.GetMatchupResultByContestId();

            var result = await _connection.QuerySingleOrDefaultAsync<MatchupResult>(
                sql,
                new { ContestId = contestId },
                commandType: CommandType.Text
            );

            return result ?? throw new Exception("Not found");
        }

        public async Task<List<Guid>> GetFinalizedContestIds(Guid seasonWeekId)
        {
            const string sql = @"
                SELECT ""Id""
                FROM public.""Contest""
                WHERE ""FinalizedUtc"" IS NOT NULL
                  AND ""SeasonWeekId"" = @SeasonWeekId;
            ";

            var results = await _connection.QueryAsync<Guid>(
                sql,
                new { SeasonWeekId = seasonWeekId },
                commandType: CommandType.Text
            );

            return results.ToList();
        }

        public async Task<FranchiseSeasonModelStatsDto> GetFranchiseSeasonStatsForPreview(Guid franchiseSeasonId)
        {
            var sql = _queryProvider.GetFranchiseSeasonStatisticsForPreviewGeneration();

            var rawStats = (await _connection.QueryAsync<FranchiseSeasonRawStat>(
                sql,
                new { FranchiseSeasonId = franchiseSeasonId },
                commandType: CommandType.Text
            )).ToList(); // fully materialize once

            if (!rawStats.Any())
            {
                _logger.LogError("Stats not found for FranchiseSeasonId={FranchiseSeasonId}", franchiseSeasonId);
                return new FranchiseSeasonModelStatsDto();
            }

            return MapToModelStats(rawStats) ?? throw new Exception("Stat mapping failed");
        }

        public async Task<List<ContestResultDto>> GetContestResultsByContestIds(List<Guid> contestIds)
        {
            var sql = _queryProvider.GetContestResultsByContestIds();

            var results = await _connection.QueryAsync<ContestResultDto>(
                sql,
                new { ContestIds = contestIds }, // contestIds = List<Guid>
                commandType: CommandType.Text
            );

            return results.ToList();
        }

        public async Task<RankingsByPollIdByWeekDto> GetRankingsByPollIdByWeek(
            string pollType,
            int seasonYear,
            int weekNumber)
        {
            var sql = _queryProvider.GetRankingsByPollBySeasonByWeek();

            var entries = await _connection.QueryAsync<RankingsByPollIdByWeekEntryDto>(
                sql,
                new
                {
                    PollType = pollType,
                    WeekNumber = weekNumber,
                    SeasonYear = seasonYear
                },
                commandType: CommandType.Text
            );

            var result = new RankingsByPollIdByWeekDto()
            {
                PollName = pollType,
                PollId = pollType,
                SeasonYear = seasonYear,
                Week = weekNumber,
                PollDateUtc = entries.FirstOrDefault()?.PollDateUtc ?? DateTime.MinValue,
                Entries = entries.ToList(),
            };

            return result;
        }

        private FranchiseSeasonModelStatsDto MapToModelStats(List<FranchiseSeasonRawStat> stats)
        {
            var dict = stats
                .GroupBy(s => s.Statistic)
                .ToDictionary(g => g.Key, g => g.First());

            double? Get(string key) => dict.TryGetValue(key, out var s) ? s.PerGameValue : null;
            double? Div(double? a, double? b) => (a.HasValue && b.HasValue && b != 0) ? a / b : null;
            int? ToInt(double? val) => val.HasValue ? (int?)Convert.ToInt32(val.Value) : null;

            return new FranchiseSeasonModelStatsDto
            {
                PointsPerGame = Get("totalPointsPerGame"),
                YardsPerGame = Get("totalYardsFromScrimmage"),
                PassingYardsPerGame = Get("passingYards"),
                RushingYardsPerGame = Get("rushingYards"),
                ThirdDownConvPct = Get("thirdDownConvPct"),
                RedZoneScoringPct = Get("redzoneScoringPct"),
                TurnoverDifferential = Get("turnOverDifferential"),

                PenaltiesPerGame = Div(Get("totalPenalties"), Get("teamGamesPlayed")),
                PenaltyYardsPerGame = Div(Get("totalPenaltyYards"), Get("teamGamesPlayed")),
                AvgYardsPerPlay = Div(Get("totalYardsFromScrimmage"), Get("totalOffensivePlays")),

                Sacks = ToInt(Get("sacks")),
                Interceptions = ToInt(Get("interceptions")),
                FumblesLost = ToInt(Get("fumblesLost")),
                Takeaways = ToInt(Get("totalTakeaways"))
            };
        }

        public async Task<FranchiseSeasonStatisticDto> GetFranchiseSeasonStatistics(Guid franchiseSeasonId)
        {
            var sql = _queryProvider.GetFranchiseSeasonStatistics();

            var entries = (await _connection.QueryAsync<FranchiseSeasonStatisticDto.FranchiseSeasonStatisticEntry>(
                sql,
                new { FranchiseSeasonId = franchiseSeasonId },
                commandType: CommandType.Text
            )).ToList();

            var dto = new FranchiseSeasonStatisticDto
            {
                GamesPlayed = 0, // TODO: Set this when you’re ready to pull it from somewhere
                Statistics = entries
                    .GroupBy(e => e.Category)
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToList()
                    )
            };

            return dto;
        }

        public async Task<List<Guid>> GetCompletedFbsContestIdsBySeasonWeekId(Guid seasonWeekId)
        {
            var sql = _queryProvider.GetCompletedFbsContestIdsBySeasonWeekId();

            var contestIds = (await _connection.QueryAsync<Guid>(
                sql,
                new { SeasonWeekId = seasonWeekId },
                commandType: CommandType.Text
            )).ToList();

            return contestIds;
        }

        public async Task<Matchup?> GetMatchupByContestId(Guid contestId)
        {
            var sql = _queryProvider.GetMatchupByContestId();

            try
            {
                var result = await _connection.QuerySingleOrDefaultAsync<Matchup>(
                    sql,
                    new { ContestId = contestId },
                    commandType: CommandType.Text);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get matchup for ContestId={ContestId}", contestId);
                throw;
            }
        }
    }
}