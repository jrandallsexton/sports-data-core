/**
 * Jest setup — runs before each test file.
 *
 * Mocks AsyncStorage using the official mock shipped by
 * @react-native-async-storage/async-storage. The real module expects the
 * native module to be registered, which isn't the case in Node-based Jest
 * runs. Without this mock, anything that transitively imports AsyncStorage
 * (e.g. our ThemeContext via LoadingSpinner) blows up with
 * "NativeModule: AsyncStorage is null."
 */
jest.mock('@react-native-async-storage/async-storage', () =>
  require('@react-native-async-storage/async-storage/jest/async-storage-mock')
);

/**
 * Mock Sentry — the real SDK pulls in native modules and would warn about
 * an undefined DSN under Jest. Tests don't need real telemetry; they need
 * the call surface available as no-ops so production code paths that
 * `Sentry.setUser(...)` / `Sentry.captureException(...)` don't blow up.
 */
jest.mock('@sentry/react-native', () => ({
  init: jest.fn(),
  wrap: (component) => component,
  setUser: jest.fn(),
  captureException: jest.fn(),
  captureMessage: jest.fn(),
  addBreadcrumb: jest.fn(),
}));
