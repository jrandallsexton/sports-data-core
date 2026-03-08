import { apiClient } from './client';
import type { Standing, UserDto } from '@/src/types/models';

export const standingsApi = {
  // GET /ui/leaderboard/{leagueId}
  getByLeague: (leagueId: string) =>
    apiClient.get<Standing[]>(`/ui/leaderboard/${leagueId}`),

  // GET /user/me  — returns userDto with leagues, profile info
  getMe: () =>
    apiClient.get<UserDto>('/user/me'),
};
