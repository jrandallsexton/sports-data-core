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
import {
  useLeagueCreationGates,
  formatGateDateOrSoon,
} from '@/src/hooks/useLeagueCreationGates';

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

  // Active creation gates keyed by backend Sport enum: { FootballNcaa: opensUtc }.
  // A gated sport's create CTA becomes a disabled "opens {date}". See
  // docs/features/league-creation-availability-gate.md.
  const gates = useLeagueCreationGates();

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
  // Every surfaced sport is gated from creation → don't urge "spin up a league
  // now" when no CTA can act on it. (Live sports are never active gates.)
  const allGated = phrases.every((s) => Boolean(gates[s.sportEnum]));
  // Frame the per-sport countdowns as *kickoff* dates so "NCAAFB in 35 days"
  // isn't misread against the earlier "Opens Aug 18" league-creation gate. Drop
  // "KICKOFFS" once everything's underway (nothing is counting down anymore).
  const seasonLabel = seasonYear ? `${seasonYear} SEASON` : 'UPCOMING SEASON';
  const eyebrow = allLive ? seasonLabel : `${seasonLabel} KICKOFFS`;

  const body = allLive
    ? 'Jump into your leagues and lock in your picks before the next kickoff.'
    : allGated
      ? "Leagues open soon - we'll be ready before Week\u00A01."
      : seasonYear
        ? `Spin up your ${seasonYear} pick'em league now so you're ready for Week\u00A01.`
        : "Spin up your pick'em league now so you're ready for Week\u00A01.";

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
            if (isLive) {
              return (
                <Button
                  key={s.key}
                  title={`Pick ${s.label} games`}
                  onPress={() => router.push('/(tabs)/picks' as never)}
                  size="md"
                  style={styles.actionButton}
                />
              );
            }

            // Creation gated (e.g. NCAAFB awaiting AP Poll release) — show when it
            // opens instead of a create action. The server enforces the same gate.
            const opensUtc = gates[s.sportEnum];
            if (opensUtc) {
              return (
                <Button
                  key={s.key}
                  // Two balanced lines — "{sport} Leagues" on top, "Open {date}"
                  // below — reads cleaner than one string that wraps mid-phrase at
                  // half-width on a phone, and matches web's "leagues open" wording.
                  title={`${s.label} Leagues\nOpen ${formatGateDateOrSoon(opensUtc)}`}
                  onPress={() => {}}
                  disabled
                  // Muted outline (not a solid primary fill) so it reads as an
                  // informational "coming soon" chip, not a tappable CTA — matching
                  // web's gated affordance.
                  variant="secondary"
                  size="md"
                  style={{ ...styles.actionButton, borderColor: theme.border, opacity: 0.75 }}
                  textStyle={{ ...styles.gatedCtaText, color: theme.textMuted }}
                />
              );
            }

            return (
              <Button
                key={s.key}
                title={`Create ${s.label} league`}
                onPress={() =>
                  router.push(
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
  // Gated "Opens {date}" CTA: two centered lines, smaller/tighter than the
  // default md label so both fit the half-width button on a phone.
  gatedCtaText: {
    fontSize: 13,
    lineHeight: 17,
    textAlign: 'center',
  },
});
