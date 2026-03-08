import { apiClient } from './client';
import type { ContestOverviewDto } from '@/src/types/models';

export const contestOverviewApi = {
  getOverview: (contestId: string) =>
    apiClient.get<ContestOverviewDto>(`/ui/contest/${contestId}/overview`),
};
