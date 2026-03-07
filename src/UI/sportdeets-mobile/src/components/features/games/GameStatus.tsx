import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { useColorScheme } from 'react-native';
import { useRouter } from 'expo-router';
import { getTheme } from '@/constants/Colors';
import type { Matchup } from '@/src/types/models';

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatTime(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

// ─── Component ───────────────────────────────────────────────────────────────

interface GameStatusProps {
  matchup: Matchup;
  /** If provided, called when the user taps on a FINAL/completed game row (preserves full nav context from caller). */
  onPressGameDetail?: () => void;
}

/**
 * Renders the center status strip of a MatchupCard.
 *
 * States:
 *   Scheduled   – game time, broadcasts, venue
 *   InProgress  – pulsing LIVE dot + period/clock + possession 🏈 + score + 🎉 TOUCHDOWN!
 *   Final       – FINAL label + score (tappable → game detail)
 *   Other       – raw status string (postponed, cancelled, etc.)
 */
export function GameStatus({ matchup, onPressGameDetail }: GameStatusProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();

  const status = matchup.status.toLowerCase();

  // ── Scheduled ──────────────────────────────────────────────────────────────
  if (status === 'scheduled') {
    return (
      <View style={styles.statusSection}>
        <Text style={[styles.statusTime, { color: theme.text }]}>
          {formatTime(matchup.startDateUtc)}
        </Text>
        {matchup.broadcasts ? (
          <Text style={[styles.statusMeta, { color: theme.textMuted }]}>
            {matchup.broadcasts}
          </Text>
        ) : null}
        {matchup.venue ? (
          <Text style={[styles.statusMeta, { color: theme.textMuted }]} numberOfLines={1}>
            {[matchup.venue, matchup.venueCity, matchup.venueState].filter(Boolean).join(', ')}
          </Text>
        ) : null}
      </View>
    );
  }

  // ── In Progress ────────────────────────────────────────────────────────────
  if (status === 'inprogress' || status === 'ongoing' || status === 'halftime') {
    const awayScore = matchup.awayScore ?? 0;
    const homeScore = matchup.homeScore ?? 0;
    const awayHasPossession =
      !!matchup.possessionFranchiseSeasonId &&
      matchup.possessionFranchiseSeasonId === matchup.awayFranchiseSeasonId;
    const homeHasPossession =
      !!matchup.possessionFranchiseSeasonId &&
      matchup.possessionFranchiseSeasonId === matchup.homeFranchiseSeasonId;

    return (
      <View style={styles.statusSection}>
        {/* LIVE indicator + period/clock */}
        <View style={styles.liveRow}>
          <View style={styles.liveDot} />
          <Text style={styles.liveText}>LIVE</Text>
          {matchup.period && matchup.clock ? (
            <Text style={[styles.clockText, { color: theme.textMuted }]}>
              {matchup.period} – {matchup.clock}
            </Text>
          ) : null}
        </View>

        {/* Score with possession indicators */}
        <View style={styles.scoreRow}>
          {awayHasPossession ? <Text style={styles.possessionIcon}>🏈</Text> : null}
          <Text style={[styles.scoreText, { color: theme.text }]}>
            {matchup.awayShort} {awayScore} – {homeScore} {matchup.homeShort}
          </Text>
          {homeHasPossession ? <Text style={styles.possessionIcon}>🏈</Text> : null}
        </View>

        {/* Scoring play flash */}
        {matchup.isScoringPlay ? (
          <Text style={styles.scoringPlayText}>🎉 TOUCHDOWN!</Text>
        ) : null}
      </View>
    );
  }

  // ── Final ──────────────────────────────────────────────────────────────────
  if (status === 'final' || status === 'completed') {
    const awayScore = matchup.awayScore ?? 0;
    const homeScore = matchup.homeScore ?? 0;

    return (
      <TouchableOpacity
        style={styles.statusSection}
        onPress={onPressGameDetail ?? (() => router.push(`/game/${matchup.contestId}` as never))}
        activeOpacity={0.7}
      >
        <Text style={[styles.statusLabel, { color: theme.textMuted }]}>FINAL</Text>
        <Text style={[styles.scoreText, { color: theme.text }]}>
          {matchup.awayShort} {awayScore} – {homeScore} {matchup.homeShort}
        </Text>
      </TouchableOpacity>
    );
  }

  // ── Other (postponed, cancelled, etc.) ────────────────────────────────────
  return (
    <View style={styles.statusSection}>
      <Text style={[styles.statusLabel, { color: theme.error }]}>{matchup.status}</Text>
    </View>
  );
}

// ─── Styles ──────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  statusSection: {
    alignItems: 'center',
    paddingVertical: 6,
    paddingHorizontal: 14,
    gap: 2,
  },
  statusTime: {
    fontSize: 13,
    fontWeight: '600',
  },
  statusMeta: {
    fontSize: 11,
  },
  statusLabel: {
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  // Live
  liveRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  liveDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#DC2626',
  },
  liveText: {
    fontSize: 12,
    fontWeight: '800',
    color: '#DC2626',
    letterSpacing: 1,
  },
  clockText: {
    fontSize: 12,
    fontWeight: '500',
  },
  // Score
  scoreRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    marginTop: 2,
  },
  scoreText: {
    fontSize: 15,
    fontWeight: '700',
  },
  possessionIcon: {
    fontSize: 14,
  },
  scoringPlayText: {
    fontSize: 13,
    fontWeight: '700',
    color: '#FACC15',
    marginTop: 2,
  },
});
