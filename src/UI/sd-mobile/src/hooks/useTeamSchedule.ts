import { useQuery } from '@tanstack/react-query';
import { teamCardApi } from '@/src/services/api/teamCardApi';
import type { TeamCardScheduleGame } from '@/src/types/models';

export const teamScheduleKeys = {
  bySlugSeasonAsOf: (
    sport: string,
    league: string,
    slug: string,
    season: number,
    asOfDate: string | null,
  ) => ['teamSchedule', sport, league, slug, season, asOfDate] as const,
};

/**
 * Fetch the slim completed-games schedule for a team. When `asOfDate` is set
 * (ISO 8601 from LeagueWeekMatchupsDto.asOfDate = SeasonWeek.EndDate of the
 * displayed week), the server applies an inclusive FinalizedUtc cutoff so the
 * MiniSchedule mirrors what was knowable at pick-time. `enabled` gates the
 * fetch so MatchupCard rows in a feed don't burn requests for schedules the
 * user never expands.
 */
export function useTeamSchedule(
  slug: string | null | undefined,
  seasonYear: number | null | undefined,
  sport: string = 'football',
  league: string = 'ncaa',
  enabled: boolean = true,
  asOfDate: string | null = null,
) {
  return useQuery<TeamCardScheduleGame[]>({
    queryKey: teamScheduleKeys.bySlugSeasonAsOf(
      sport,
      league,
      slug ?? '',
      seasonYear ?? 0,
      asOfDate,
    ),
    queryFn: () =>
      teamCardApi
        .getSchedule(slug!, seasonYear!, sport, league, asOfDate)
        .then((r) => {
          const body = r.data as unknown;
          // Some apiClient setups unwrap to a { data: T } envelope. Handle both.
          const wrapped = body as { data?: TeamCardScheduleGame[] };
          return Array.isArray(wrapped.data)
            ? wrapped.data
            : Array.isArray(body)
              ? (body as TeamCardScheduleGame[])
              : [];
        }),
    enabled: enabled && !!slug && !!seasonYear,
    staleTime: 1000 * 60 * 5,
  });
}
