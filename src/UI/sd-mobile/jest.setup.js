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
