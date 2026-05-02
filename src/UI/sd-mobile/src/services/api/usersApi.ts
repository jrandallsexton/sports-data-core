import { apiClient } from './client';
import type { UserDto } from '@/src/types/models';

/**
 * Wrapper for /user/* endpoints. Mirrors the web app's `src/api/usersApi.js`.
 * `getMe` duplicates `standingsApi.getMe` deliberately so user-related calls
 * have a self-describing home; both backends hit the same endpoint.
 */
export const usersApi = {
  // GET /user/me → UserDto (timezone, leagues, profile bits)
  getMe: () => apiClient.get<UserDto>('/user/me'),

  // PATCH /user/me/timezone — accepts any IANA zone or null/empty to clear.
  // Backend validates via TimeZoneInfo.TryFindSystemTimeZoneById.
  updateTimezone: (timezone: string | null) =>
    apiClient.patch('/user/me/timezone', { timezone }),
};
