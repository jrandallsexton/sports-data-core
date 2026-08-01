import React, { useEffect, useState } from 'react';
import type { StyleProp, TextStyle } from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { COUNTDOWN_WINDOW_MS, joinClosesState } from './joinDisplay';

// setTimeout treats delays above 2^31-1 ms (~24.8 days) as 0.
const MAX_TIMEOUT_MS = 2 ** 31 - 1;
const TICK_MS = 60 * 1000;

interface Props {
  closesAtUtc: string | null | undefined;
  isJoinable: boolean;
  style?: StyleProp<TextStyle>;
}

/**
 * Join-status label mirroring sd-ui's JoinClosesLabel: live minute-tick
 * countdown inside 10 days, plain date beyond, "Closed" once past. Arms a
 * boundary timer so a long-lived screen transitions date -> countdown ->
 * Closed without a reload.
 */
export function JoinClosesLabel({ closesAtUtc, isJoinable, style }: Props) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const [now, setNow] = useState(() => Date.now());

  const closesMs = closesAtUtc ? new Date(closesAtUtc).getTime() : NaN;
  const remaining = closesMs - now;
  const inCountdown =
    Number.isFinite(closesMs) && remaining > 0 && remaining <= COUNTDOWN_WINDOW_MS;

  useEffect(() => {
    if (!Number.isFinite(closesMs) || remaining <= 0) return undefined;
    if (inCountdown) {
      const id = setInterval(() => setNow(Date.now()), TICK_MS);
      return () => clearInterval(id);
    }
    // Outside the window: one timer aimed at the boundary.
    const untilWindow = Math.min(remaining - COUNTDOWN_WINDOW_MS, MAX_TIMEOUT_MS);
    const id = setTimeout(() => setNow(Date.now()), Math.max(untilWindow, TICK_MS));
    return () => clearTimeout(id);
  }, [closesMs, inCountdown, remaining]);

  const state = joinClosesState(closesAtUtc, isJoinable, now);
  // Countdown draws attention; every other state is muted.
  const color = state.kind === 'countdown' ? theme.tint : theme.textMuted;

  return <Text style={[{ color, fontWeight: state.kind === 'countdown' ? '600' : '400' }, style]}>{state.text}</Text>;
}
