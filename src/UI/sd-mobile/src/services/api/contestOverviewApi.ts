import { apiClient } from './client';
import type { ContestOverviewDto } from '@/src/types/models';

export const contestOverviewApi = {
  // The endpoint is sport-aware: the API uses the sport/league query params
  // to pick which sport's data to query. Without them it defaults to
  // football/ncaa, which is why MLB contests came back empty.
  getOverview: (contestId: string, sport: string, league: string) =>
    apiClient.get<ContestOverviewDto>(`/ui/contest/${contestId}/overview`, {
      params: { sport, league },
    }),
};
