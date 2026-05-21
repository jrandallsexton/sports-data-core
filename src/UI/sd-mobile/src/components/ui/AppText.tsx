import React from 'react';
import { Text as RNText, StyleSheet, type TextProps } from 'react-native';
import { useTextScale } from '@/src/lib/textSize/TextSizeContext';

/**
 * Canonical `<Text>` for the app. Drop-in replacement for React Native's
 * `Text` that multiplies any `fontSize` in `style` by the user's current
 * text-size preference (S/M/L → 1.0 / 1.125 / 1.25).
 *
 * Why this exists: React Native has no parent-to-child font-size cascade,
 * so retro-fitting a single user setting onto ~150 `fontSize:` declarations
 * across 23 files requires either a wrapper at render time or a token
 * migration. The wrapper is mechanical to introduce; the migration would be
 * a multi-week effort. See docs/mobile/text-size-setting.md for the
 * decision rationale.
 *
 * Why `allowFontScaling={false}`: iOS Settings → Display → Text Size
 * (Dynamic Type) and our in-app S/M/L would otherwise compound — a user
 * on iOS "XXL" who picks "L" would get a surprise giant result. Our
 * setting is the single source of truth for in-app text. The OS-level
 * setting still scales text outside our app.
 *
 * Opt-out: brand surfaces (the `<Wordmark>` lockup, e.g.) import `Text`
 * directly from `react-native` so their size stays fixed regardless of
 * the user's preference.
 */
export function Text(props: TextProps) {
  const scale = useTextScale();
  const flat = StyleSheet.flatten(props.style);
  const scaled =
    flat && typeof flat.fontSize === 'number'
      ? { ...flat, fontSize: flat.fontSize * scale }
      : flat;
  return <RNText allowFontScaling={false} {...props} style={scaled} />;
}
