import { apiClient } from './client';
import type { NotificationPreferences, UserDto } from '@/src/types/models';

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

  // PATCH /user/me/username — unique handle ([a-z0-9_], 3–30, case-insensitive).
  // Backend rejects invalid/taken handles with a 4xx.
  updateUsername: (username: string) =>
    apiClient.patch('/user/me/username', { username }),

  // PATCH /user/me/displayname — free-text label (non-unique, max 25).
  updateDisplayName: (displayName: string) =>
    apiClient.patch('/user/me/displayname', { displayName }),

  // DELETE /user/me — deletes the account. Server anonymizes the record and
  // removes the Firebase login; caller signs out afterward.
  deleteAccount: () => apiClient.delete('/user/me'),

  // GET /user/me/notification-preferences → full flag set (all-enabled defaults
  // when the user has never changed a setting).
  getNotificationPreferences: () =>
    apiClient.get<NotificationPreferences>('/user/me/notification-preferences'),

  // PATCH /user/me/notification-preferences — full replacement; send every flag.
  updateNotificationPreferences: (prefs: NotificationPreferences) =>
    apiClient.patch('/user/me/notification-preferences', prefs),
};
