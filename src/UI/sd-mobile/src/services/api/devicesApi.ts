import { apiClient } from './client';

export interface RegisterDevicePayload {
  installationId: string;
  fcmToken: string;
  platform: 'ios' | 'android';
}

/**
 * Wrapper for /ui/devices. Registration is idempotent server-side on the
 * device's stable installationId, so it's safe to call on every launch / token
 * refresh; registering under a new account reassigns the device to that user.
 */
export const devicesApi = {
  // POST /ui/devices → 204
  registerDevice: (payload: RegisterDevicePayload) =>
    apiClient.post<void>('/ui/devices', payload),

  // DELETE /ui/devices/{installationId} → 204. Called best-effort at sign-out
  // so the signed-out user stops receiving pushes on this device.
  unregisterDevice: (installationId: string) =>
    apiClient.delete<void>(`/ui/devices/${encodeURIComponent(installationId)}`),
};
