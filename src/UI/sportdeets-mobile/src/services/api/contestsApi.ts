import { apiClient } from './client';
import type { League } from '@/src/types/models';

export const leaguesApi = {
  // Leagues come from /user/me (userDto.leagues) - no separate endpoint needed
  // GET /ui/leagues/{id}/overview/{week} if needed later
  getOverview: (leagueId: string, week: number) =>
    apiClient.get<{ data: League }>(`/ui/leagues/${leagueId}/overview/${week}`),
};
