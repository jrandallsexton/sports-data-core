import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';
import { Appearance, useColorScheme as useSystemColorScheme } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import type { ColorScheme } from '@/constants/Colors';

/**
 * Theme mode the user has selected. `system` follows the OS-level light/dark
 * setting; the other two pin the theme regardless of OS. Matches web parity
 * where a user-explicit preference wins over the OS default.
 */
export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'theme-mode';
const VALID_MODES: ThemeMode[] = ['light', 'dark', 'system'];

interface ThemeContextValue {
  mode: ThemeMode;
  setMode: (mode: ThemeMode) => void;
  resolvedScheme: ColorScheme;
  /**
   * False until the persisted theme preference has been read from AsyncStorage.
   * Consumers that render theme-dependent pixels should wait on this before
   * revealing UI — otherwise a user with a stored `light` preference sees a
   * brief `system`-resolved flash before hydration completes. The splash
   * screen controller in app/_layout.tsx uses this as its gate.
   */
  isHydrated: boolean;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

function isValidMode(value: unknown): value is ThemeMode {
  return typeof value === 'string' && (VALID_MODES as string[]).includes(value);
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const systemScheme = useSystemColorScheme();
  const [mode, setModeState] = useState<ThemeMode>('system');
  const [isHydrated, setIsHydrated] = useState(false);

  // Load persisted preference on mount. Any parse failure silently falls back
  // to `system` — we'd rather ship with OS defaults than crash on corrupt
  // storage from a prior app version. `isHydrated` flips true regardless of
  // success/failure so the splash can un-gate. The `mounted` flag guards
  // against setState-after-unmount (matters for fast teardown in tests or
  // provider remounts during HMR).
  useEffect(() => {
    let mounted = true;
    AsyncStorage.getItem(STORAGE_KEY)
      .then((raw) => {
        if (!mounted) return;
        if (isValidMode(raw)) setModeState(raw);
      })
      .catch(() => {
        // ignore — keep default
      })
      .finally(() => {
        if (mounted) setIsHydrated(true);
      });
    return () => {
      mounted = false;
    };
  }, []);

  const setMode = useCallback((next: ThemeMode) => {
    setModeState(next);
    AsyncStorage.setItem(STORAGE_KEY, next).catch(() => {
      // persistence is best-effort; in-memory state is authoritative for the session
    });
  }, []);

  // In `system` mode, resolve against the current OS setting. `Appearance`
  // emits change events handled by `useSystemColorScheme`, so a toggle in
  // iOS/Android settings is reflected without an app restart.
  const resolvedScheme: ColorScheme = useMemo(() => {
    if (mode === 'light' || mode === 'dark') return mode;
    return (systemScheme as ColorScheme) ?? (Appearance.getColorScheme() as ColorScheme) ?? 'light';
  }, [mode, systemScheme]);

  const value = useMemo<ThemeContextValue>(
    () => ({ mode, setMode, resolvedScheme, isHydrated }),
    [mode, setMode, resolvedScheme, isHydrated],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

/** Expose mode + setter for the theme selector UI. */
export function useThemeMode(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) {
    throw new Error('useThemeMode must be used within a <ThemeProvider>');
  }
  return ctx;
}

/**
 * Drop-in replacement for React Native's `useColorScheme`. Returns the
 * resolved scheme respecting the user's explicit mode choice, not the raw
 * OS setting. Components that already call `useColorScheme()` just swap
 * the import path.
 */
export function useColorScheme(): ColorScheme {
  const ctx = useContext(ThemeContext);
  // Fallback to raw OS value if provider isn't mounted yet (e.g. in tests).
  const systemScheme = useSystemColorScheme();
  if (ctx) return ctx.resolvedScheme;
  return (systemScheme as ColorScheme) ?? 'light';
}
