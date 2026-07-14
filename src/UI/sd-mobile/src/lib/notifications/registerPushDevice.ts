import { Platform } from 'react-native';
import * as Sentry from '@sentry/react-native';

import { getFcmToken, getFcmTokenIfGranted, type PermissionStatus } from './pushNotifications';
import { getOrCreateInstallationId } from '@/src/lib/device/installationId';
import { devicesApi } from '@/src/services/api/devicesApi';

export interface RegistrationOutcome {
  ok: boolean;
  permissionStatus: PermissionStatus;
  error?: string;
}

/**
 * Registers this device's FCM token with the API — the single place device
 * registration happens (used by both the auto-register hook and the manual
 * "register this device" settings action).
 *
 * - <c>prompt=false</c> (default): silent. Only registers when permission is
 *   already granted, so it never fires an unsolicited iOS prompt.
 * - <c>prompt=true</c>: may request permission first, so the manual action can
 *   grant + register in one tap.
 *
 * Failures are reported to Sentry (<c>area: push-registration</c>) so a device
 * that won't register is diagnosable — the observability gap that made the
 * missing-iPad-row case invisible. See
 * docs/mobile/device-registration-resilience.md.
 */
export async function registerThisDevice(
  { prompt = false }: { prompt?: boolean } = {}
): Promise<RegistrationOutcome> {
  if (Platform.OS === 'web') {
    return { ok: false, permissionStatus: 'undetermined' };
  }

  const { token, permissionStatus, error } = prompt
    ? await getFcmToken()
    : await getFcmTokenIfGranted();

  if (!token) {
    // Permission simply not granted is a normal silent no-op. But permission
    // granted with no token (e.g. APNs not ready, RN-Firebase error) is a real
    // failure worth surfacing.
    if (permissionStatus === 'granted') {
      Sentry.captureMessage('Push registration: no FCM token despite granted permission', {
        level: 'warning',
        tags: { area: 'push-registration', prompt: String(prompt) },
        extra: { error, permissionStatus },
      });
    }
    return { ok: false, permissionStatus, error: error ?? undefined };
  }

  try {
    const installationId = await getOrCreateInstallationId();
    const platform = Platform.OS === 'ios' ? 'ios' : 'android';
    await devicesApi.registerDevice({ installationId, fcmToken: token, platform });
    return { ok: true, permissionStatus };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    Sentry.captureException(err, {
      tags: { area: 'push-registration', prompt: String(prompt) },
      extra: { stage: 'post', permissionStatus },
    });
    return { ok: false, permissionStatus, error: message };
  }
}
