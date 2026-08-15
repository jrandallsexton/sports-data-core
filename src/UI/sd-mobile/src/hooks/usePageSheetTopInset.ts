import { useContext } from 'react';
import { Platform, StatusBar } from 'react-native';
import { SafeAreaInsetsContext } from 'react-native-safe-area-context';

/**
 * Top padding for full-screen Modal content on Android.
 *
 * `presentationStyle="pageSheet"` is an iOS-only prop: iOS presents the
 * sheet below the status bar, but Android ignores it and renders the
 * Modal edge-to-edge, so a modal's header row (title + close button)
 * lands underneath the system status bar icons. Every page-sheet modal
 * applies this inset as `paddingTop` on its root container.
 *
 * Returns 0 on iOS (the native sheet already clears the status bar).
 * On Android, prefers the safe-area inset and falls back to
 * `StatusBar.currentHeight` for the rare host where the modal window
 * reports a zero inset. Reads the insets context directly (not
 * useSafeAreaInsets, which THROWS with no provider) so component tests
 * can render modal-bearing trees without a SafeAreaProvider — the
 * StatusBar fallback covers that path.
 */
export function usePageSheetTopInset(): number {
  const insets = useContext(SafeAreaInsetsContext);
  if (Platform.OS !== 'android') return 0;
  return Math.max(insets?.top ?? 0, StatusBar.currentHeight ?? 0);
}
