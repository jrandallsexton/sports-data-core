import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { Button } from '@/src/components/ui/Button';

// Kickoff dates mirror web's PrimarySlotOffSeasonCountdown — keep in sync.
// NCAAFB: first weekend of September. NFL: Thursday after Labor Day.
const SPORTS = [
  {
    key: 'NCAAFB',
    label: 'NCAAFB',
    kickoff: new Date(Date.UTC(2026, 8, 5)),
    sportEnum: 'FootballNcaa',
  },
  {
    key: 'NFL',
    label: 'NFL',
    kickoff: new Date(Date.UTC(2026, 8, 10)),
    sportEnum: 'FootballNfl',
  },
] as const;

function daysUntil(targetUtc: Date, nowMs: number = Date.now()): number {
  const msPerDay = 1000 * 60 * 60 * 24;
  return Math.ceil((targetUtc.getTime() - nowMs) / msPerDay);
}

type SportPhrase = { status: 'live' | 'upcoming'; text: string };

function sportPhrase(sport: (typeof SPORTS)[number], nowMs: number): SportPhrase {
  const days = daysUntil(sport.kickoff, nowMs);
  if (days <= 0) return { status: 'live', text: `${sport.label} is underway` };
  return {
    status: 'upcoming',
    text: `${sport.label} in ${days} ${days === 1 ? 'day' : 'days'}`,
  };
}

/**
 * Tier 1 primary slot — user has at least one league but no sport they care
 * about is currently in-season. Per-sport countdown lines read like a
 * scoreboard rather than a comma-run-on. Each sport gets its own CTA:
 *   - upcoming → create-league with ?sport= preselected
 *   - live     → picks tab (all leagues)
 */
export function PrimarySlotOffSeasonCountdown() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();

  const nowMs = Date.now();
  const phrases = SPORTS.map((s) => ({ ...s, phrase: sportPhrase(s, nowMs) }));
  const allLive = phrases.every((s) => s.phrase.status === 'live');

  const body = allLive
    ? 'Jump into your leagues and lock in your picks before the next kickoff.'
    : "Spin up your 2026 pick'em league now so you're ready for Week 1.";

  return (
    <View
      style={[
        styles.card,
        { backgroundColor: theme.card, borderColor: theme.border },
      ]}
    >
      <Text style={[styles.eyebrow, { color: theme.tint }]}>2026 SEASON</Text>

      {allLive ? (
        <Text style={[styles.headline, { color: theme.text }]}>
          NCAAFB and NFL are underway — pick your week
        </Text>
      ) : (
        <View style={styles.headlineLines}>
          {phrases.map((s) => (
            <Text key={s.key} style={[styles.headline, { color: theme.text }]}>
              {s.phrase.text}
            </Text>
          ))}
        </View>
      )}

      <Text style={[styles.body, { color: theme.textMuted }]}>{body}</Text>

      <View style={styles.actions}>
        {allLive ? (
          <Button
            title="Go to picks"
            onPress={() => router.push('/(tabs)/picks' as never)}
            size="md"
            fullWidth
          />
        ) : (
          phrases.map((s) => {
            const isLive = s.phrase.status === 'live';
            return (
              <Button
                key={s.key}
                title={isLive ? `Pick ${s.label} games` : `Create ${s.label} league`}
                onPress={() =>
                  isLive
                    ? router.push('/(tabs)/picks' as never)
                    : router.push(
                        {
                          pathname: '/create-league',
                          params: { sport: s.sportEnum },
                        } as never,
                      )
                }
                size="md"
                fullWidth
              />
            );
          })
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 20,
    alignItems: 'center',
  },
  eyebrow: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    marginBottom: 8,
  },
  headline: {
    fontSize: 22,
    fontWeight: '700',
    textAlign: 'center',
    lineHeight: 28,
  },
  headlineLines: {
    gap: 2,
    marginBottom: 2,
  },
  body: {
    fontSize: 14,
    lineHeight: 20,
    textAlign: 'center',
    marginTop: 10,
    marginBottom: 16,
    maxWidth: 440,
  },
  actions: {
    width: '100%',
    gap: 8,
  },
});
