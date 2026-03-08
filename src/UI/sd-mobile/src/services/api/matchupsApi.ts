import { apiClient } from './client';
import type { LeagueMatchupsResponse, PreviewResponse } from '@/src/types/models';

export const matchupsApi = {
  // GET /ui/leagues/{leagueId}/matchups/{week}
  getByLeagueAndWeek: (leagueId: string, week: number) =>
    apiClient.get<LeagueMatchupsResponse>(`/ui/leagues/${leagueId}/matchups/${week}`),

  // GET /ui/matchup/{contestId}/preview
  getPreview: (contestId: string) =>
    apiClient.get<PreviewResponse>(`/ui/matchup/${encodeURIComponent(contestId)}/preview`),
};
