using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamCard;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Infrastructure.Data.Canonical;

public interface IProvideCanonicalData
{
    Task<TeamCardDto?> GetTeamCard(GetTeamCardQuery query, CancellationToken cancellationToken = default);

    Task<Dictionary<string, Guid>> GetFranchiseIdsBySlugsAsync(Sport sport, List<string> slugs);

    Task<Dictionary<Guid, string>> GetConferenceIdsBySlugsAsync(Sport sport, int seasonYear, List<string> slugs);

    Task<List<ConferenceDivisionNameAndSlugDto>> GetConferenceNamesAndSlugsForSeasonYear(int seasonYear);

    Task<List<SeasonWeek>> GetCompletedSeasonWeeks(int seasonYear);

    Task<SeasonWeek?> GetCurrentSeasonWeek();

    Task<List<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetricsBySeasonYear(int seasonYear);

    Task<List<Matchup>> GetMatchupsForCurrentWeek();

    Task<List<Matchup>> GetMatchupsForSeasonWeek(int seasonYear, int seasonWeekNumber);

    Task<List<LeagueWeekMatchupsDto.MatchupForPickDto>> GetMatchupsByContestIds(List<Guid> contestIds);

    Task<MatchupForPreviewDto> GetMatchupForPreview(Guid contestId);

    Task<Dictionary<Guid, MatchupForPreviewDto>> GetMatchupsForPreview(IReadOnlyCollection<Guid> contestIds, CancellationToken cancellationToken = default);

    Task<MatchupResult> GetMatchupResult(Guid contestId);

    Task<List<Guid>> GetFinalizedContestIds(Guid seasonWeekId);

    Task<FranchiseSeasonModelStatsDto> GetFranchiseSeasonStatsForPreview(Guid franchiseSeasonId);

    Task<List<ContestResultDto>> GetContestResultsByContestIds(List<Guid> contestIds);

    Task<RankingsByPollIdByWeekDto> GetRankingsByPollIdByWeek(string pollType, int seasonYear, int weekNumber);

    Task<FranchiseSeasonStatisticDto> GetFranchiseSeasonStatistics(Guid franchiseSeasonId);

    Task<List<SeasonWeek>> GetCurrentAndLastWeekSeasonWeeks();

    Task<List<FranchiseSeasonCompetitionResultDto>> GetFranchiseSeasonCompetitionResultsByFranchiseSeasonId(Guid franchiseSeasonId);

    Task RefreshContestMediaByContestId(Guid contestId);

    Task<FranchiseSeasonMetricsDto> GetFranchiseSeasonMetrics(Guid franchiseSeasonId);

    Task<List<FranchiseSeasonPollDto>> GetFranchiseSeasonRankings(int seasonYear);

    Task<List<Guid>> GetCompletedFbsContestIdsBySeasonWeekId(Guid seasonWeekId);

    Task<Matchup?> GetMatchupByContestId(Guid contestId);
}