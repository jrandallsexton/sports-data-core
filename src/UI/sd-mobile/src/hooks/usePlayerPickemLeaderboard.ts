import { useQuery } from '@tanstack/react-query';
import { useAuthStore } from '@/src/stores/authStore';
import {
  getStandings,
  getLeagueWeekLineups,
  type PlayerStandings,
  type LeagueWeekLineups,
} from '@/src/services/api/playerPickemApi';

const keys = {
  standings: (leagueId: string, seasonYear: number) =>
    ['playerPickem', 'standings', leagueId, seasonYear] as const,
  weekLineups: (leagueId: string, seasonYear: number, week: number) =>
    ['playerPickem', 'weekLineups', leagueId, seasonYear, week] as const,
};

/** Player Pick'em season standings (Standings tab, player leagues). */
export function usePlayerStandings(leagueId: string | null, seasonYear: number | null) {
  const { user, isInitialized } = useAuthStore();
  return useQuery<PlayerStandings>({
    queryKey: keys.standings(leagueId ?? '', seasonYear ?? 0),
    queryFn: () => getStandings(leagueId!, seasonYear!),
    enabled: isInitialized && !!user && !!leagueId && !!seasonYear,
    staleTime: 1000 * 30,
  });
}

/** Every member's lineup for one week (By Week pane, player leagues). */
export function useLeagueWeekLineups(
  leagueId: string | null,
  seasonYear: number | null,
  week: number | null,
) {
  const { user, isInitialized } = useAuthStore();
  return useQuery<LeagueWeekLineups>({
    queryKey: keys.weekLineups(leagueId ?? '', seasonYear ?? 0, week ?? 0),
    queryFn: () => getLeagueWeekLineups(leagueId!, seasonYear!, week!),
    enabled: isInitialized && !!user && !!leagueId && !!seasonYear && !!week,
    staleTime: 1000 * 30,
    // Others' slots unlock at their game's kickoff-5; staleTime alone never
    // refetches an open pane, so poll while anything is still hidden.
    refetchInterval: (query) =>
      query.state.data?.members.some((m) => m.hiddenSlotCount > 0) ? 60_000 : false,
  });
}
