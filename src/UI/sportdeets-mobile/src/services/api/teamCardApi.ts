import { apiClient } from './client';

/**
 * Fetches team statistics for the team card / comparison feature.
 *
 * GET /ui/teamcard/sport/{sport}/league/{league}/team/{slug}/{year}/statistics
 *       ?franchiseSeasonId={franchiseSeasonId}
 */
export const teamCardApi = {
  getStatistics: (
    slug: string,
    year: number,
    franchiseSeasonId: string,
    sport = 'football',
    league = 'ncaa',
  ) =>
    apiClient.get(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${encodeURIComponent(slug)}/${year}/statistics?franchiseSeasonId=${encodeURIComponent(franchiseSeasonId)}`,
    ),

  getMetrics: (
    slug: string,
    year: number,
    franchiseSeasonId: string,
    sport = 'football',
    league = 'ncaa',
  ) =>
    apiClient.get(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${encodeURIComponent(slug)}/${year}/metrics?franchiseSeasonId=${encodeURIComponent(franchiseSeasonId)}`,
    ),
};
