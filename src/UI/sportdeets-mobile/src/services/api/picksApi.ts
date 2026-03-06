import { apiClient } from './client';
import type { UserPick, PickType, PickWidgetResponse } from '@/src/types/models';

export interface SubmitPickPayload {
  pickemGroupId: string;      // leagueId
  contestId: string;          // matchup's contestId
  pickType: PickType;
  franchiseSeasonId: string;  // picked team's franchiseSeasonId
  week: number;
  confidencePoints?: number;
}

export const picksApi = {
  // GET /ui/picks/{leagueId}/week/{week}
  getByLeagueAndWeek: (leagueId: string, week: number) =>
    apiClient.get<UserPick[]>(`/ui/picks/${leagueId}/week/${week}`),

  // POST /ui/picks
  submitPick: (payload: SubmitPickPayload) =>
    apiClient.post<void>('/ui/picks', payload),

  // GET /ui/picks/{year}/widget  — season-to-date pick record for the current user
  getWidget: (year = 2025) =>
    apiClient.get<PickWidgetResponse>(`/ui/picks/${year}/widget`),
};
