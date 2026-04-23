import { useQuery } from '@tanstack/react-query';
import { teamCardApi } from '@/src/services/api/teamCardApi';
import type { TeamCardDto } from '@/src/types/models';

export const teamCardKeys = {
  bySlugAndSeason: (sport: string, league: string, slug: string, season: number) =>
    ['teamCard', sport, league, slug, season] as const,
};

/**
 * Fetch a team card by (sport, league, slug, seasonYear). Sport and league
 * default to football/ncaa for legacy callers but new sport-aware routes
 * should pass them explicitly — different sports can share a slug
 * ("boise-state-broncos" is only unambiguous when paired with football/ncaa).
 */
export function useTeamCard(
  slug: string | null | undefined,
  seasonYear: number | null | undefined,
  sport: string = 'football',
  league: string = 'ncaa',
) {
  return useQuery<TeamCardDto>({
    queryKey: teamCardKeys.bySlugAndSeason(sport, league, slug ?? '', seasonYear ?? 0),
    queryFn: () =>
      teamCardApi.getBySlugAndSeason(slug!, seasonYear!, sport, league).then((r) => {
        const body = r.data as unknown;
        const wrapped = body as { data?: TeamCardDto };
        return wrapped.data ?? (body as TeamCardDto);
      }),
    enabled: !!slug && !!seasonYear,
    staleTime: 1000 * 60 * 5, // 5 min — schedule data doesn't change rapidly
  });
}
