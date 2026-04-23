import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { Button } from '@/src/components/ui/Button';

/**
 * Tier 1 primary slot — shown when the signed-in user has no league
 * memberships yet. Mirrors web's PrimarySlotNewUser, minus the "Browse
 * public leagues" CTA (no discover screen on mobile yet).
 */
export function PrimarySlotNewUser() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();

  return (
    <View
      style={[
        styles.card,
        { backgroundColor: theme.card, borderColor: theme.border },
      ]}
    >
      <Text style={[styles.eyebrow, { color: theme.tint }]}>WELCOME TO SPORTDEETS</Text>
      <Text style={[styles.headline, { color: theme.text }]}>
        Pick the 2026 season with friends
      </Text>
      <Text style={[styles.body, { color: theme.textMuted }]}>
        Start your own pick'em league in under a minute.
      </Text>
      <View style={styles.actions}>
        <Button
          title="Create a league"
          onPress={() => router.push('/create-league' as never)}
          size="lg"
          fullWidth
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 24,
    alignItems: 'center',
  },
  eyebrow: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    marginBottom: 10,
  },
  headline: {
    fontSize: 22,
    fontWeight: '700',
    textAlign: 'center',
    lineHeight: 28,
    marginBottom: 10,
  },
  body: {
    fontSize: 14,
    lineHeight: 20,
    textAlign: 'center',
    marginBottom: 18,
    maxWidth: 440,
  },
  actions: {
    width: '100%',
  },
});
