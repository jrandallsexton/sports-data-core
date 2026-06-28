import { apiClient } from './client';

export interface RegisterDevicePayload {
  fcmToken: string;
  platform: 'ios' | 'android';
}

/**
 * Wrapper for /ui/devices. Registers (or refreshes) this device's FCM token
 * for the authenticated user. Server-side upsert is idempotent on
 * (UserId, FcmToken), so it's safe to call on every launch / token refresh.
 */
export const devicesApi = {
  // POST /ui/devices → 204
  registerDevice: (payload: RegisterDevicePayload) =>
    apiClient.post<void>('/ui/devices', payload),
};
