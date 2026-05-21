import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';

/**
 * User-selected text size. Maps to a font-size multiplier applied by the
 * canonical `<Text>` wrapper in src/components/ui/AppText. Default is
 * `small` so existing users see no visual change when the feature ships.
 *
 * Mirrors `ThemeContext` line-for-line — persistence, hydration gate,
 * hook shape, and provider mounting pattern are identical so the two
 * settings stay structurally interchangeable for future readers.
 */
export type TextSize = 'small' | 'medium' | 'large';

const STORAGE_KEY = 'text-size';
const VALID_SIZES: TextSize[] = ['small', 'medium', 'large'];

/**
 * Scale multipliers per size. 12.5% / 25% steps mirror web accessibility
 * conventions ("100/112/125") — tight enough that layouts don't break,
 * visible enough that the user feels a real difference between steps.
 */
const SCALE_FOR: Record<TextSize, number> = {
  small: 1.0,
  medium: 1.125,
  large: 1.25,
};

interface TextSizeContextValue {
  size: TextSize;
  setSize: (size: TextSize) => void;
  /** Multiplier derived from `size`. Components shouldn't need this directly — use `useTextScale()`. */
  scale: number;
  /**
   * False until the persisted text-size preference has been read from
   * AsyncStorage. The splash screen controller in app/_layout.tsx waits
   * on this (alongside the theme `isHydrated`) so the first painted
   * pixels already reflect the stored preference.
   */
  isHydrated: boolean;
}

const TextSizeContext = createContext<TextSizeContextValue | null>(null);

function isValidSize(value: unknown): value is TextSize {
  return typeof value === 'string' && (VALID_SIZES as string[]).includes(value);
}

export function TextSizeProvider({ children }: { children: React.ReactNode }) {
  const [size, setSizeState] = useState<TextSize>('small');
  const [isHydrated, setIsHydrated] = useState(false);

  // Load persisted preference on mount. Any parse failure silently falls
  // back to `small` — corrupt storage shouldn't crash the app. The
  // `mounted` flag guards setState-after-unmount in tests / HMR teardown.
  useEffect(() => {
    let mounted = true;
    AsyncStorage.getItem(STORAGE_KEY)
      .then((raw) => {
        if (!mounted) return;
        if (isValidSize(raw)) setSizeState(raw);
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

  const setSize = useCallback((next: TextSize) => {
    setSizeState(next);
    AsyncStorage.setItem(STORAGE_KEY, next).catch(() => {
      // persistence is best-effort; in-memory state is authoritative for the session
    });
  }, []);

  const value = useMemo<TextSizeContextValue>(
    () => ({ size, setSize, scale: SCALE_FOR[size], isHydrated }),
    [size, setSize, isHydrated],
  );

  return <TextSizeContext.Provider value={value}>{children}</TextSizeContext.Provider>;
}

/** Exposes mode + setter + hydration flag for the settings selector UI. */
export function useTextSize(): TextSizeContextValue {
  const ctx = useContext(TextSizeContext);
  if (!ctx) {
    throw new Error('useTextSize must be used within a <TextSizeProvider>');
  }
  return ctx;
}

/**
 * Returns the current font-size multiplier. Used by the `<AppText>`
 * wrapper. Falls back to 1.0 if the provider isn't mounted (e.g. in
 * tests rendering a component in isolation) so consumers don't crash
 * out of context.
 */
export function useTextScale(): number {
  const ctx = useContext(TextSizeContext);
  return ctx ? ctx.scale : 1.0;
}
