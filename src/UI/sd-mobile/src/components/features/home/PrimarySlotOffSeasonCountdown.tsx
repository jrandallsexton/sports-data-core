import React, { useEffect, useMemo, useState } from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { Button } from '@/src/components/ui/Button';

// ─── Kickoff helpers ──────────────────────────────────────────────────────────
//
// NCAAFB: first Saturday of September. NFL: Thursday after Labor Day (Labor
// Day = first Monday of September, kickoff = that Monday + 3 days). Computed
// at render time so the card rolls over on its own each season — no yearly
// constant maintenance.

/** First Saturday of September in the given year, 00:00 UTC. */
function ncaafbKickoff(year: number): Date {
  for (let day = 1; day <= 7; day++) {
    const d = new Date(Date.UTC(year, 8, day)); // month 8 = September
    if (d.getUTCDay() === 6) return d; // Saturday
  }
  return new Date(Date.UTC(year, 8, 7)); // unreachable fallback
}

/** Thursday after Labor Day (first Monday of September) in the given year. */
function nflKickoff(year: number): Date {
  for (let day = 1; day <= 7; day++) {
    const d = new Date(Date.UTC(year, 8, day));
    if (d.getUTCDay() === 1) {
      // Labor Day — kickoff is the following Thursday (+3 days).
      return new Date(Date.UTC(year, 8, day + 3));
    }
  }
  return new Date(Date.UTC(year, 8, 10)); // unreachable fallback
}

/**
 * Return the season year the countdown should track.
 *
 * Anchors on the *previous* calendar year's NFL kickoff so January/February
 * stays on the in-progress season (playoffs + Super Bowl) instead of
 * incorrectly advancing to the next year's off-season countdown. Rollover
 * happens ≈6 months after that kickoff — roughly March of the current
 * calendar year — which is the product-correct moment to start counting
 * down to the new season.
 *
 * Examples:
 *   - Nov 15, 2026 (regular season):       year=2026 ✓
 *   - Jan 15, 2027 (2026 playoffs):        year=2026 ✓ ("underway" still)
 *   - Apr 15, 2027 (off-season):           year=2027 ✓ (counting down)
 *   - Sep 11, 2027 (2027 NCAAFB started):  year=2027 ✓
 */
function targetSeasonYear(nowMs: number): number {
  const now = new Date(nowMs);
  const year = now.getUTCFullYear();
  const rollover = new Date(nflKickoff(year - 1));
  rollover.setUTCMonth(rollover.getUTCMonth() + 6);
  return nowMs >= rollover.getTime() ? year : year - 1;
}

function daysUntil(targetUtc: Date, nowMs: number): number {
  const msPerDay = 1000 * 60 * 60 * 24;
  return Math.ceil((targetUtc.getTime() - nowMs) / msPerDay);
}

type SportPhrase = { status: 'live' | 'upcoming'; text: string };

function sportPhrase(
  sport: { label: string; kickoff: Date },
  nowMs: number,
): SportPhrase {
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
 *
 * `nowMs` ticks hourly so day-boundary transitions update without a remount —
 * an app left open across midnight still shows the correct "X days" count.
 * Hourly (vs daily) is cheap and covers DST / straggler tick drift for free.
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

  const year = useMemo(() => targetSeasonYear(nowMs), [nowMs]);
  const sports = useMemo(
    () => [
      {
        key: 'NCAAFB',
        label: 'NCAAFB',
        kickoff: ncaafbKickoff(year),
        sportEnum: 'FootballNcaa',
      },
      {
        key: 'NFL',
        label: 'NFL',
        kickoff: nflKickoff(year),
        sportEnum: 'FootballNfl',
      },
    ],
    [year],
  );

  const phrases = sports.map((s) => ({ ...s, phrase: sportPhrase(s, nowMs) }));
  const allLive = phrases.every((s) => s.phrase.status === 'live');

  const body = allLive
    ? 'Jump into your leagues and lock in your picks before the next kickoff.'
    : `Spin up your ${year} pick'em league now so you're ready for Week 1.`;

  return (
    <View
      style={[
        styles.card,
        { backgroundColor: theme.card, borderColor: theme.border },
      ]}
    >
      <Text style={[styles.eyebrow, { color: theme.tint }]}>{year} SEASON</Text>

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
