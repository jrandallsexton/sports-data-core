import { apiClient } from './client';
import type { TeamCardDto, TeamCardScheduleGame } from '@/src/types/models';

export const teamCardApi = {
  /** GET /ui/teamcard/sport/football/league/ncaa/team/{slug}/{year} */
  getBySlugAndSeason: (slug: string, seasonYear: number, sport = 'football', league = 'ncaa') =>
    apiClient.get<TeamCardDto>(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${encodeURIComponent(slug)}/${seasonYear}`,
    ),

  /**
   * GET /ui/teamcard/.../{year}/schedule[?asOfDate=ISO]
   *
   * Slim schedule — completed games only, newest-first. `asOfDate`
   * (LeagueWeekMatchupsDto.asOfDate = SeasonWeek.EndDate of the displayed
   * week) is an inclusive FinalizedUtc upper bound so a Week-N pick-review
   * view doesn't show results the picker couldn't yet have known about.
   */
  getSchedule: (
    slug: string,
    seasonYear: number,
    sport = 'football',
    league = 'ncaa',
    asOfDate?: string | null,
  ) => {
    const base = `/ui/teamcard/sport/${sport}/league/${league}/team/${encodeURIComponent(slug)}/${seasonYear}/schedule`;
    const url = asOfDate ? `${base}?asOfDate=${encodeURIComponent(asOfDate)}` : base;
    return apiClient.get<TeamCardScheduleGame[]>(url);
  },

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
