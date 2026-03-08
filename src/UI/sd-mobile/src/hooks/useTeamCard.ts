import { useQuery } from '@tanstack/react-query';
import { teamCardApi } from '@/src/services/api/teamCardApi';
import type { TeamCardDto } from '@/src/types/models';

export const teamCardKeys = {
  bySlugAndSeason: (slug: string, season: number) =>
    ['teamCard', slug, season] as const,
};

export function useTeamCard(
  slug: string | null | undefined,
  seasonYear: number | null | undefined,
) {
  return useQuery<TeamCardDto>({
    queryKey: teamCardKeys.bySlugAndSeason(slug ?? '', seasonYear ?? 0),
    queryFn: () =>
      teamCardApi.getBySlugAndSeason(slug!, seasonYear!).then((r) => {
        const body = r.data as unknown;
        const wrapped = body as { data?: TeamCardDto };
        return wrapped.data ?? (body as TeamCardDto);
      }),
    enabled: !!slug && !!seasonYear,
    staleTime: 1000 * 60 * 5, // 5 min — schedule data doesn't change rapidly
  });
}
