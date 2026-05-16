import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Image } from 'react-native';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import type { Matchup } from '@/src/types/models';
import { formatToUserTime } from '@/src/utils/timeUtils';
import { useUserTimeZone } from '@/src/hooks/useUserTimeZone';

// ─── Component ───────────────────────────────────────────────────────────────

interface GameStatusProps {
  matchup: Matchup;
  /**
   * Backend Sport enum name ("FootballNcaa" | "FootballNfl" | "BaseballMlb").
   * Used to dispatch the InProgress UI; baseball renders inning + count +
   * runners + at-bat / pitcher, football renders period + clock +
   * possession. Defaults to football for unknown / missing values so
   * callers that don't yet thread it through don't regress.
   */
  leagueSport?: string | null;
  /** If provided, called when the user taps on a FINAL/completed game row (preserves full nav context from caller). */
  onPressGameDetail?: () => void;
}

/**
 * Renders the center status strip of a MatchupCard.
 *
 * States:
 *   Scheduled   – game time, broadcasts, venue
 *   InProgress  – dispatched by leagueSport:
 *                   BaseballMlb → inning + count + outs + runners + at-bat
 *                   default     → LIVE dot + period/clock + possession 🏈
 *   Final       – FINAL label + score (tappable → game detail)
 *   Other       – raw status string (postponed, cancelled, etc.)
 */
export function GameStatus({ matchup, leagueSport, onPressGameDetail }: GameStatusProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const userTz = useUserTimeZone();

  const status = matchup.status.toLowerCase();

  // ── Scheduled ──────────────────────────────────────────────────────────────
  if (status === 'scheduled') {
    return (
      <View style={styles.statusSection}>
        <Text style={[styles.statusTime, { color: theme.text }]}>
          {formatToUserTime(matchup.startDateUtc, userTz)}
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
    if (leagueSport === 'BaseballMlb') {
      return <BaseballInProgress matchup={matchup} theme={theme} />;
    }
    return <FootballInProgress matchup={matchup} theme={theme} />;
  }

  // ── Final ──────────────────────────────────────────────────────────────────
  if (status === 'final' || status === 'completed') {
    const awayScore = matchup.awayScore ?? 0;
    const homeScore = matchup.homeScore ?? 0;

    return (
      <TouchableOpacity
        style={styles.statusSection}
        onPress={onPressGameDetail}
        activeOpacity={onPressGameDetail ? 0.7 : 1}
        disabled={!onPressGameDetail}
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

// ─── InProgress sub-components ───────────────────────────────────────────────

type Theme = ReturnType<typeof getTheme>;

function FootballInProgress({ matchup, theme }: { matchup: Matchup; theme: Theme }) {
  const awayScore = matchup.awayScore ?? 0;
  const homeScore = matchup.homeScore ?? 0;
  const awayHasPossession =
    !!matchup.possessionFranchiseSeasonId &&
    matchup.possessionFranchiseSeasonId === matchup.awayFranchiseSeasonId;
  const homeHasPossession =
    !!matchup.possessionFranchiseSeasonId &&
    matchup.possessionFranchiseSeasonId === matchup.homeFranchiseSeasonId;
  const hasLastPlay =
    typeof matchup.lastPlayDescription === 'string' &&
    matchup.lastPlayDescription.length > 0;

  return (
    <View style={styles.statusSection}>
      <View style={styles.liveRow}>
        <View style={styles.liveDot} />
        <Text style={styles.liveText}>LIVE</Text>
        {matchup.period && matchup.clock ? (
          <Text style={[styles.clockText, { color: theme.textMuted }]}>
            {matchup.period} – {matchup.clock}
          </Text>
        ) : null}
      </View>

      <View style={[styles.scoreRow, matchup.isScoringPlay ? styles.scoreRowFlash : null]}>
        {awayHasPossession ? <Text style={styles.possessionIcon}>🏈</Text> : null}
        <Text style={[styles.scoreText, { color: theme.text }]}>
          {matchup.awayShort} {awayScore} – {homeScore} {matchup.homeShort}
        </Text>
        {homeHasPossession ? <Text style={styles.possessionIcon}>🏈</Text> : null}
      </View>

      {matchup.isScoringPlay ? (
        <Text style={styles.scoringPlayText}>🎉 TOUCHDOWN!</Text>
      ) : null}

      {hasLastPlay ? (
        <Text
          style={[styles.lastPlayText, { color: theme.textMuted }]}
          numberOfLines={2}
        >
          {matchup.lastPlayDescription}
        </Text>
      ) : null}
    </View>
  );
}

function BaseballInProgress({ matchup, theme }: { matchup: Matchup; theme: Theme }) {
  const awayScore = matchup.awayScore ?? 0;
  const homeScore = matchup.homeScore ?? 0;
  // halfInning "Top" → away batting (gets the ⚾); "Bottom" → home batting.
  const half = (matchup.halfInning ?? '').toLowerCase();
  const awayIsBatting = half === 'top';
  const homeIsBatting = half === 'bottom';

  // Per-slot team logo: batter wears the batting team's logo, pitcher
  // wears the defensive team's. Either may be null when halfInning is
  // missing or when the team has no logo URL — slot tolerates absence.
  const batterLogoUri = awayIsBatting
    ? matchup.awayLogoUri
    : homeIsBatting
      ? matchup.homeLogoUri
      : null;
  const pitcherLogoUri = awayIsBatting
    ? matchup.homeLogoUri
    : homeIsBatting
      ? matchup.awayLogoUri
      : null;

  const hasInningRow =
    (typeof matchup.inning === 'number' && matchup.inning > 0) ||
    (typeof matchup.halfInning === 'string' && matchup.halfInning.length > 0);
  const hasRunnersRow = !!(
    matchup.runnerOnFirst || matchup.runnerOnSecond || matchup.runnerOnThird
  );
  const hasAtBatRow = !!(matchup.atBatShortName || matchup.pitchingShortName);
  const hasLastPlay =
    typeof matchup.lastPlayDescription === 'string' &&
    matchup.lastPlayDescription.length > 0;

  const outsLabel = matchup.outs === 1 ? 'out' : 'outs';
  const formattedHalfInning =
    matchup.halfInning && matchup.inning
      ? `${matchup.halfInning} ${matchup.inning}`
      : matchup.halfInning || (matchup.inning ? `Inning ${matchup.inning}` : '');

  return (
    <View style={styles.statusSection}>
      <View style={styles.liveRow}>
        <View style={styles.liveDot} />
        <Text style={styles.liveText}>LIVE</Text>
      </View>

      <View style={[styles.scoreRow, matchup.isScoringPlay ? styles.scoreRowFlash : null]}>
        {awayIsBatting ? <Text style={styles.possessionIcon}>⚾</Text> : null}
        <Text style={[styles.scoreText, { color: theme.text }]}>
          {matchup.awayShort} {awayScore} – {homeScore} {matchup.homeShort}
        </Text>
        {homeIsBatting ? <Text style={styles.possessionIcon}>⚾</Text> : null}
      </View>

      {hasAtBatRow ? (
        <View style={styles.baseballAtBatRow}>
          {matchup.atBatShortName ? (
            <View style={styles.baseballAtBatSlot}>
              {batterLogoUri ? (
                <Image
                  source={{ uri: batterLogoUri }}
                  style={styles.baseballAtBatLogo}
                  resizeMode="contain"
                  accessibilityIgnoresInvertColors
                />
              ) : null}
              {matchup.atBatHeadshotUrl ? (
                <Image
                  source={{ uri: matchup.atBatHeadshotUrl }}
                  style={styles.baseballAtBatHeadshot}
                  accessibilityIgnoresInvertColors
                />
              ) : null}
              <Text style={[styles.baseballAtBatText, { color: theme.text }]}>
                AB: {matchup.atBatShortName}
                {matchup.atBatPositionAbbreviation
                  ? ` (${matchup.atBatPositionAbbreviation})`
                  : ''}
              </Text>
            </View>
          ) : null}
          {matchup.pitchingShortName ? (
            <View style={styles.baseballAtBatSlot}>
              {pitcherLogoUri ? (
                <Image
                  source={{ uri: pitcherLogoUri }}
                  style={styles.baseballAtBatLogo}
                  resizeMode="contain"
                  accessibilityIgnoresInvertColors
                />
              ) : null}
              {matchup.pitchingHeadshotUrl ? (
                <Image
                  source={{ uri: matchup.pitchingHeadshotUrl }}
                  style={styles.baseballAtBatHeadshot}
                  accessibilityIgnoresInvertColors
                />
              ) : null}
              <Text style={[styles.baseballAtBatText, { color: theme.textMuted }]}>
                P: {matchup.pitchingShortName}
                {matchup.pitchingPositionAbbreviation
                  ? ` (${matchup.pitchingPositionAbbreviation})`
                  : ''}
              </Text>
            </View>
          ) : null}
        </View>
      ) : null}

      {hasInningRow ? (
        <Text style={[styles.baseballSummary, { color: theme.textMuted }]}>
          {formattedHalfInning}
          {' · '}
          {matchup.balls ?? 0}-{matchup.strikes ?? 0}
          {' · '}
          {matchup.outs ?? 0} {outsLabel}
        </Text>
      ) : null}

      {hasRunnersRow ? (
        <Text style={[styles.baseballRunners, { color: theme.textMuted }]}>
          Runners:
          {matchup.runnerOnFirst ? ' 1B' : ''}
          {matchup.runnerOnSecond ? ' 2B' : ''}
          {matchup.runnerOnThird ? ' 3B' : ''}
        </Text>
      ) : null}

      {hasLastPlay ? (
        <Text
          style={[styles.lastPlayText, { color: theme.textMuted }]}
          numberOfLines={2}
        >
          {matchup.lastPlayDescription}
        </Text>
      ) : null}
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
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 6,
  },
  // Brief yellow highlight on the score row when isScoringPlay is true.
  // The store auto-clears the flag after 2s (see contestUpdatesStore),
  // so this style toggles off automatically without an Animated value.
  scoreRowFlash: {
    backgroundColor: 'rgba(250, 204, 21, 0.25)',
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
  // Sport-neutral last-play row — used by both football and baseball.
  lastPlayText: {
    fontSize: 11,
    fontStyle: 'italic',
    textAlign: 'center',
    marginTop: 2,
  },
  // Baseball-specific InProgress rows
  baseballAtBatRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'center',
    columnGap: 12,
    rowGap: 4,
    marginTop: 2,
  },
  baseballAtBatSlot: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  baseballAtBatLogo: {
    width: 18,
    height: 18,
  },
  baseballAtBatHeadshot: {
    width: 24,
    height: 24,
    borderRadius: 12,
  },
  baseballAtBatText: {
    fontSize: 12,
    fontWeight: '600',
  },
  baseballSummary: {
    fontSize: 12,
    fontWeight: '500',
    marginTop: 2,
  },
  baseballRunners: {
    fontSize: 12,
    fontWeight: '500',
  },
});
