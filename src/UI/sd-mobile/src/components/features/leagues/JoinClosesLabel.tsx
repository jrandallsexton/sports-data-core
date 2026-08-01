import React from 'react';
import type { StyleProp, TextStyle } from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { useLiveJoinState } from './useLiveJoinState';

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
  const state = useLiveJoinState(closesAtUtc, isJoinable);
  // Countdown draws attention; every other state is muted.
  const color = state.kind === 'countdown' ? theme.tint : theme.textMuted;

  return <Text style={[{ color, fontWeight: state.kind === 'countdown' ? '600' : '400' }, style]}>{state.text}</Text>;
}
