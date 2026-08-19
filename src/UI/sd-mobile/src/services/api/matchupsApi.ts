import { apiClient } from './client';
import type { ContestHistory, LeagueMatchupsResponse, PreviewResponse } from '@/src/types/models';

export const matchupsApi = {
  // GET /ui/leagues/{leagueId}/matchups/{week}
  getByLeagueAndWeek: (leagueId: string, week: number) =>
    apiClient.get<LeagueMatchupsResponse>(`/ui/leagues/${leagueId}/matchups/${week}`),

  // GET /ui/matchup/{contestId}/preview
  getPreview: (contestId: string) =>
    apiClient.get<PreviewResponse>(`/ui/matchup/${encodeURIComponent(contestId)}/preview`),

  /**
   * GET /api/{sport}/{league}/contests/{contestId}/history
   *
   * Historical context for a matchup: last N head-to-head meetings and each
   * team's late-prior-season form — the same blocks the preview/insight
   * models consume. Preview-safe semantics (finalized only, preseason
   * excluded, no as-of leakage) are baked in server-side.
   */
  getContestHistory: (sport: string, league: string, contestId: string) =>
    apiClient.get<ContestHistory>(
      `/api/${sport}/${league}/contests/${encodeURIComponent(contestId)}/history`,
    ),
};
