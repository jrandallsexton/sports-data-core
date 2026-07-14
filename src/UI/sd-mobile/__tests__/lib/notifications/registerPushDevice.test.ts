import * as Sentry from '@sentry/react-native';

import { registerThisDevice } from '@/src/lib/notifications/registerPushDevice';
import { getFcmToken, getFcmTokenIfGranted } from '@/src/lib/notifications/pushNotifications';
import { getOrCreateInstallationId } from '@/src/lib/device/installationId';
import { devicesApi } from '@/src/services/api/devicesApi';

jest.mock('@/src/lib/notifications/pushNotifications', () => ({
  getFcmToken: jest.fn(),
  getFcmTokenIfGranted: jest.fn(),
}));
jest.mock('@/src/lib/device/installationId', () => ({
  getOrCreateInstallationId: jest.fn(),
}));
jest.mock('@/src/services/api/devicesApi', () => ({
  devicesApi: { registerDevice: jest.fn(), unregisterDevice: jest.fn() },
}));

const mockGranted = getFcmTokenIfGranted as jest.Mock;
const mockPrompt = getFcmToken as jest.Mock;
const mockInstallId = getOrCreateInstallationId as jest.Mock;
const mockRegister = devicesApi.registerDevice as jest.Mock;
const mockCaptureMessage = Sentry.captureMessage as jest.Mock;
const mockCaptureException = Sentry.captureException as jest.Mock;

describe('registerThisDevice', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockInstallId.mockResolvedValue('install-123');
  });

  it('registers when a token is available (silent path)', async () => {
    mockGranted.mockResolvedValue({ token: 'fcm-abc', permissionStatus: 'granted', error: null });
    mockRegister.mockResolvedValue(undefined);

    const outcome = await registerThisDevice();

    expect(outcome.ok).toBe(true);
    expect(mockRegister).toHaveBeenCalledWith(
      expect.objectContaining({ installationId: 'install-123', fcmToken: 'fcm-abc' })
    );
    expect(mockCaptureMessage).not.toHaveBeenCalled();
    expect(mockCaptureException).not.toHaveBeenCalled();
  });

  it('warns to Sentry when permission is granted but no token was obtained', async () => {
    mockGranted.mockResolvedValue({
      token: null,
      permissionStatus: 'granted',
      error: 'FCM returned empty token',
    });

    const outcome = await registerThisDevice();

    expect(outcome.ok).toBe(false);
    expect(mockRegister).not.toHaveBeenCalled();
    expect(mockCaptureMessage).toHaveBeenCalledTimes(1);
  });

  it('is a silent no-op (no Sentry) when permission is not granted', async () => {
    mockGranted.mockResolvedValue({ token: null, permissionStatus: 'undetermined', error: null });

    const outcome = await registerThisDevice();

    expect(outcome.ok).toBe(false);
    expect(mockRegister).not.toHaveBeenCalled();
    expect(mockCaptureMessage).not.toHaveBeenCalled();
    expect(mockCaptureException).not.toHaveBeenCalled();
  });

  it('captures a Sentry exception when the POST throws', async () => {
    mockGranted.mockResolvedValue({ token: 'fcm-abc', permissionStatus: 'granted', error: null });
    mockRegister.mockRejectedValue(new Error('network down'));

    const outcome = await registerThisDevice();

    expect(outcome.ok).toBe(false);
    expect(outcome.error).toBe('network down');
    expect(mockCaptureException).toHaveBeenCalledTimes(1);
  });

  it('uses the prompting token fn when prompt=true', async () => {
    mockPrompt.mockResolvedValue({ token: 'fcm-xyz', permissionStatus: 'granted', error: null });
    mockRegister.mockResolvedValue(undefined);

    const outcome = await registerThisDevice({ prompt: true });

    expect(outcome.ok).toBe(true);
    expect(mockPrompt).toHaveBeenCalled();
    expect(mockGranted).not.toHaveBeenCalled();
  });
});
