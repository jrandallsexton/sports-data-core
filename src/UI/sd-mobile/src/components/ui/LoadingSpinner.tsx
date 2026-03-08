import React from 'react';
import { View, ActivityIndicator, Text, StyleSheet } from 'react-native';
import { getTheme } from '@/constants/Colors';
import { useColorScheme } from 'react-native';

interface LoadingSpinnerProps {
  message?: string;
  size?: 'small' | 'large';
  fullScreen?: boolean;
}

export function LoadingSpinner({
  message,
  size = 'large',
  fullScreen = false,
}: LoadingSpinnerProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  return (
    <View style={[styles.container, fullScreen && styles.fullScreen]}>
      <ActivityIndicator size={size} color={theme.tint} />
      {message && <Text style={[styles.message, { color: theme.textMuted }]}>{message}</Text>}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: 24,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 12,
  },
  fullScreen: {
    flex: 1,
  },
  message: {
    fontSize: 14,
    fontWeight: '500',
  },
});
