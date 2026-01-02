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

    /// <summary>
/// Retrieves a preview representation of the matchup for the specified contest.
/// </summary>
/// <param name="contestId">The unique identifier of the contest.</param>
/// <returns>A MatchupForPreviewDto representing the matchup for the specified contest.</returns>
Task<MatchupForPreviewDto> GetMatchupForPreview(Guid contestId);

    /// <summary>
/// Retrieves preview matchup data for multiple contest IDs.
/// </summary>
/// <param name="contestIds">A collection of contest IDs to retrieve previews for.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>A dictionary mapping each found contest ID to its <see cref="MatchupForPreviewDto"/>; contest IDs that are not found are omitted.</returns>
Task<Dictionary<Guid, MatchupForPreviewDto>> GetMatchupsForPreview(IReadOnlyCollection<Guid> contestIds, CancellationToken cancellationToken = default);

    /// <summary>
/// Fetches the matchup result for the specified contest identifier.
/// </summary>
/// <param name="contestId">The unique identifier of the contest to retrieve the result for.</param>
/// <returns>The matchup result for the specified contest.</returns>
Task<MatchupResult> GetMatchupResult(Guid contestId);

    /// <summary>
/// Retrieves the contest IDs that have been finalized for the specified season week.
/// </summary>
/// <param name="seasonWeekId">The identifier of the season week to query.</param>
/// <returns>A list of contest GUIDs that are finalized for the given season week; an empty list if there are none.</returns>
Task<List<Guid>> GetFinalizedContestIds(Guid seasonWeekId);

    Task<FranchiseSeasonModelStatsDto> GetFranchiseSeasonStatsForPreview(Guid franchiseSeasonId);

    Task<List<ContestResultDto>> GetContestResultsByContestIds(List<Guid> contestIds);

    Task<RankingsByPollIdByWeekDto> GetRankingsByPollIdByWeek(string pollType, int seasonYear, int weekNumber);

    Task<FranchiseSeasonStatisticDto> GetFranchiseSeasonStatistics(Guid franchiseSeasonId);

    Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId);

    Task<List<SeasonWeek>> GetCurrentAndLastWeekSeasonWeeks();

    Task<List<FranchiseSeasonCompetitionResultDto>> GetFranchiseSeasonCompetitionResultsByFranchiseSeasonId(Guid franchiseSeasonId);

    Task RefreshContestByContestId(Guid contestId);

    Task RefreshContestMediaByContestId(Guid contestId);

    Task<FranchiseSeasonMetricsDto> GetFranchiseSeasonMetrics(Guid franchiseSeasonId);

    Task<List<FranchiseSeasonPollDto>> GetFranchiseSeasonRankings(int seasonYear);

    Task<List<Guid>> GetCompletedFbsContestIdsBySeasonWeekId(Guid seasonWeekId);

    Task<Matchup?> GetMatchupByContestId(Guid contestId);
}