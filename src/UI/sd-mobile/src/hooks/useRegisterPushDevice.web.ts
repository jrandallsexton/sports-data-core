/**
 * Web no-op for {@link useRegisterPushDevice}.
 *
 * Device push registration depends on `@react-native-firebase/messaging`, which
 * has no web implementation. Metro resolves this `.web` variant for the web
 * bundle, so the native dependency is never pulled in by the root layout's
 * static import. The native implementation lives in `useRegisterPushDevice.ts`.
 */
export function useRegisterPushDevice(): void {
  // Intentionally empty — no push registration on web.
}
