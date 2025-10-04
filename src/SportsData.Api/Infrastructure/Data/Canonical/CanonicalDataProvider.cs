﻿using Dapper;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

using System.Data;
using SportsData.Core.Infrastructure.Clients.Producer;
using static SportsData.Api.Application.UI.Rankings.Dtos.RankingsByPollIdByWeekDto;

namespace SportsData.Api.Infrastructure.Data.Canonical
{
    public interface IProvideCanonicalData
    {
        Task<TeamCardDto?> GetTeamCard(GetTeamCardQuery query, CancellationToken cancellationToken = default);

        Task<Dictionary<string, Guid>> GetFranchiseIdsBySlugsAsync(Sport sport, List<string> slugs);

        Task<Dictionary<Guid, string>> GetConferenceIdsBySlugsAsync(Sport sport, int seasonYear, List<string> slugs);

        Task<List<ConferenceDivisionNameAndSlugDto>> GetConferenceNamesAndSlugsForSeasonYear(int seasonYear);

        Task<SeasonWeek?> GetCurrentSeasonWeek();

        Task<List<Matchup>> GetMatchupsForCurrentWeek();

        Task<List<LeagueWeekMatchupsDto.MatchupForPickDto>> GetMatchupsByContestIds(List<Guid> contestIds);

        Task<MatchupForPreviewDto> GetMatchupForPreview(Guid contestId);

        Task<MatchupResult> GetMatchupResult(Guid contestId);

        Task<List<Guid>> GetFinalizedContestIds(Guid seasonWeekId);

        Task<FranchiseSeasonModelStatsDto> GetFranchiseSeasonStatsForPreview(Guid franchiseSeasonId);

        Task<List<ContestResultDto>> GetContestResultsByContestIds(List<Guid> contestIds);

        Task<RankingsByPollIdByWeekDto> GetRankingsByPollIdByWeek(string pollType, int seasonYear, int weekNumber);

        Task<FranchiseSeasonStatisticDto> GetFranchiseSeasonStatistics(Guid franchiseSeasonId);

        Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId);

        Task<List<SeasonWeek>> GetCurrentAndLastWeekSeasonWeeks();

        Task<List<FranchiseSeasonCompetitionResultDto>> GetFranchiseSeasonCompetitionResultsByFranchiseSeasonId(Guid franchiseSeasonId);
    }

    public class CanonicalDataProvider : IProvideCanonicalData
    {
        private readonly IDbConnection _connection;
        private readonly ILogger<CanonicalDataProvider> _logger;
        private readonly CanonicalDataQueryProvider _queryProvider;
        //private readonly IProvideProducers _producerClient;

        public CanonicalDataProvider(
            ILogger<CanonicalDataProvider> logger,
            IDbConnection connection,
            CanonicalDataQueryProvider queryProvider)
        {
            _logger = logger;
            _connection = connection;
            _queryProvider = queryProvider;
            //_producerClient = producerClient;
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

        public async Task<SeasonWeek?> GetCurrentSeasonWeek()
        {
            var sql = _queryProvider.GetCurrentSeasonWeek();

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

        public async Task<List<SeasonWeek>> GetCurrentAndLastWeekSeasonWeeks()
        {
            var sql = _queryProvider.GetCurrentAndLastWeekSeasonWeeks();

            try
            {
                var result = await _connection.QueryAsync<SeasonWeek>(sql);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve current season week.");
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

        public async Task<List<LeagueWeekMatchupsDto.MatchupForPickDto>> GetMatchupsByContestIds(List<Guid> contestIds)
        {
            var sql = _queryProvider.GetLeagueMatchupsByContestIds();

            var results = await _connection.QueryAsync<LeagueWeekMatchupsDto.MatchupForPickDto>(
                sql,
                new { ContestIds = contestIds }, // contestIds = List<Guid>
                commandType: CommandType.Text
            );

            return results.ToList();
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

        public async Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId)
        {
            return new ContestOverviewDto();
            //var dto = await _producerClient.GetContestOverviewByContestId(contestId);

            //return dto ?? throw new Exception("Not found");
        }
    }
}