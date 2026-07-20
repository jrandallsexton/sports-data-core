import React, { useEffect, useState } from 'react';
import { View, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { useQueries } from '@tanstack/react-query';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { Button } from '@/src/components/ui/Button';
import {
  seasonApi,
  REGULAR_SEASON_TYPE_CODE,
  type CurrentSeason,
} from '@/src/services/api/seasonApi';

// ─── Sports ─────────────────────────────────────────────────────────────────
//
// `sportEnum` is what create-league reads from ?sport= to preselect the tab.
// `sport`/`league` are the API route segments. Kickoff is data-driven — the
// Regular Season phase's StartDate from seasons/current — not a computed rule.
// The prior rules ("first Saturday of September" / "Thursday after Labor Day")
// were both wrong for 2026. See docs/features/data-driven-season-countdown.md.

const SPORTS = [
  { key: 'NCAAFB', label: 'NCAAFB', sportEnum: 'FootballNcaa', sport: 'football', league: 'ncaa' },
  { key: 'NFL', label: 'NFL', sportEnum: 'FootballNfl', sport: 'football', league: 'nfl' },
] as const;

// Kickoff = the Regular Season phase's start, or null when not sourced yet.
function regularSeasonStart(season: CurrentSeason | undefined): string | null {
  return season?.phases?.find((p) => p.typeCode === REGULAR_SEASON_TYPE_CODE)?.startDate ?? null;
}

function daysUntil(kickoffIso: string, nowMs: number): number {
  const msPerDay = 1000 * 60 * 60 * 24;
  return Math.ceil((new Date(kickoffIso).getTime() - nowMs) / msPerDay);
}

type SportPhrase = { status: 'live' | 'upcoming' | 'unknown'; text: string };

function sportPhrase(label: string, kickoff: string | null, nowMs: number): SportPhrase {
  if (!kickoff) return { status: 'unknown', text: `${label} kickoff coming soon` };
  const days = daysUntil(kickoff, nowMs);
  if (days <= 0) return { status: 'live', text: `${label} is underway` };
  return { status: 'upcoming', text: `${label} in ${days} ${days === 1 ? 'day' : 'days'}` };
}

/**
 * Tier 1 primary slot — user has at least one league but no sport they care
 * about is currently in-season. Per-sport countdown lines read like a
 * scoreboard rather than a comma-run-on. Each sport gets its own CTA:
 *   - upcoming → create-league with ?sport= preselected
 *   - live     → picks tab (all leagues)
 *
 * Kickoffs are fetched per sport from seasons/current (the Regular Season
 * phase's StartDate). `nowMs` ticks hourly so day-boundary transitions update
 * without a remount — an app left open across midnight still shows the correct
 * "X days" count.
 */
export function PrimarySlotOffSeasonCountdown() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();

  const [nowMs, setNowMs] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNowMs(Date.now()), 60 * 60 * 1000);
    return () => clearInterval(id);
  }, []);

  const results = useQueries({
    queries: SPORTS.map((s) => ({
      queryKey: ['season', 'current', s.sport, s.league],
      queryFn: () => seasonApi.getCurrentSeason(s.sport, s.league).then((r) => r.data),
      staleTime: 1000 * 60 * 60, // kickoff dates barely move; refetch hourly at most
      // A sport with no sourced season is a valid "coming soon" state — don't
      // retry it as though it were a transient error.
      retry: false,
    })),
  });

  const loading = results.some((r) => r.isLoading);

  // Cheap to recompute each render (and it must, to follow the hourly nowMs
  // tick), so no memo — a memo here would just need fragile deps over the
  // query results.
  const phrases = SPORTS.map((s, i) => {
    const kickoff = regularSeasonStart(results[i].data);
    return { ...s, kickoff, phrase: sportPhrase(s.label, kickoff, nowMs) };
  });

  const seasonYear =
    results.map((r) => r.data?.seasonYear).find((y) => y != null) ?? null;

  const allLive = phrases.every((s) => s.phrase.status === 'live');
  const eyebrow = seasonYear ? `${seasonYear} SEASON` : 'UPCOMING SEASON';

  const body = allLive
    ? 'Jump into your leagues and lock in your picks before the next kickoff.'
    : seasonYear
      ? `Spin up your ${seasonYear} pick'em league now so you're ready for Week 1.`
      : "Spin up your pick'em league now so you're ready for Week 1.";

  if (loading) {
    return (
      <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
        <Text style={[styles.body, { color: theme.textMuted }]}>Loading the season schedule…</Text>
      </View>
    );
  }

  return (
    <View
      style={[
        styles.card,
        { backgroundColor: theme.card, borderColor: theme.border },
      ]}
    >
      <Text style={[styles.eyebrow, { color: theme.tint }]}>{eyebrow}</Text>

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
            style={styles.actionButton}
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
                style={styles.actionButton}
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
    flexDirection: 'row',
    gap: 8,
  },
  // Each CTA shares the row equally → two sports read as two columns; the lone
  // all-live "Go to picks" button fills the row on its own.
  actionButton: {
    flex: 1,
  },
});
