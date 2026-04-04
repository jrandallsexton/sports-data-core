using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamCard;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.Infrastructure.Data.Canonical;

public interface IProvideCanonicalData
{
    Task<TeamCardDto?> GetTeamCard(GetTeamCardQuery query, CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, string>> GetConferenceIdsBySlugsAsync(Sport sport, int seasonYear, List<string> slugs);

    Task<List<ConferenceDivisionNameAndSlugDto>> GetConferenceNamesAndSlugsForSeasonYear(int seasonYear);

    Task<List<CanonicalSeasonWeekDto>> GetCompletedSeasonWeeks(int seasonYear);

    Task<CanonicalSeasonWeekDto?> GetCurrentSeasonWeek();

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

    Task<List<CanonicalSeasonWeekDto>> GetCurrentAndLastWeekSeasonWeeks();

    Task<List<FranchiseSeasonCompetitionResultDto>> GetFranchiseSeasonCompetitionResultsByFranchiseSeasonId(Guid franchiseSeasonId);

    Task<List<Guid>> GetCompletedFbsContestIdsBySeasonWeekId(Guid seasonWeekId);

    Task<Matchup?> GetMatchupByContestId(Guid contestId);
}
