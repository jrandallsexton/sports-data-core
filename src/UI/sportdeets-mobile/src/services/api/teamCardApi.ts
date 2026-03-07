import { apiClient } from './client';
import type { TeamCardDto } from '@/src/types/models';

export const teamCardApi = {
  /** GET /ui/teamcard/sport/football/league/ncaa/team/{slug}/{year} */
  getBySlugAndSeason: (slug: string, seasonYear: number, sport = 'football', league = 'ncaa') =>
    apiClient.get<TeamCardDto>(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${encodeURIComponent(slug)}/${seasonYear}`,
    ),

  /**
   * Fetches team statistics for the team card / comparison feature.
   *
   * GET /ui/teamcard/sport/{sport}/league/{league}/team/{slug}/{year}/statistics
   *       ?franchiseSeasonId={franchiseSeasonId}
   */
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
