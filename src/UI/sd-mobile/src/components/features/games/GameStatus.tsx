import React from 'react';
import { View, TouchableOpacity, StyleSheet, Image } from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import type { Matchup, PickType } from '@/src/types/models';
import { formatToUserTime } from '@/src/utils/timeUtils';
import { useUserTimeZone } from '@/src/hooks/useUserTimeZone';
import { FinalScoreResult } from './FinalScoreResult';

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
  /**
   * League pick mode. Drives the STATUS_FINAL quick-scan indicator:
   *   StraightUp        → inline ✓ next to the winning team's short
   *   AgainstTheSpread  → row below score reading "✓ {team} covered" or "Push"
   *   OverUnder         → row below score reading "✓ Over/Under {N}" or "Push"
   * Null / undefined falls back to StraightUp visuals so the indicator
   * doesn't silently disappear if a caller hasn't threaded it through.
   */
  pickType?: PickType | null;
}

// Mid-game paused states. Game is technically still live (score + period/
// inning are meaningful), but no play is happening. Render the InProgress
// block beneath a banner that names the reason via statusDescription.
const DELAY_STATUSES = new Set(['STATUS_DELAYED', 'STATUS_RAIN_DELAY', 'STATUS_SUSPENDED']);

// Terminal "game won't be played as scheduled" states. Same struck-through
// gameTime visual; statusDescription drives the label.
const TERMINAL_STATUSES = new Set(['STATUS_POSTPONED', 'STATUS_CANCELED']);

/**
 * Renders the center status strip of a MatchupCard.
 *
 * Branches on raw ESPN `matchup.status` (e.g. "STATUS_FINAL"); display
 * labels come from `matchup.statusDescription` (e.g. "Final") where the
 * label isn't hardcoded.
 *
 * States:
 *   STATUS_SCHEDULED       – game time, broadcasts, venue
 *   STATUS_IN_PROGRESS /
 *   STATUS_HALFTIME        – dispatched by leagueSport:
 *                              BaseballMlb → inning + count + outs + runners + at-bat
 *                              default     → LIVE dot + period/clock + possession 🏈
 *   DELAY_STATUSES         – Delayed / RainDelay / Suspended — same
 *                            InProgress block beneath a delay banner.
 *   STATUS_FINAL           – FINAL label + score (tappable → game detail)
 *   TERMINAL_STATUSES      – Postponed / Canceled — label + struck-through
 *                            gameTime + venue.
 *   Other                  – raw statusDescription (defensive fallback)
 */
export function GameStatus({ matchup, leagueSport, onPressGameDetail, pickType }: GameStatusProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const userTz = useUserTimeZone();

  const status = matchup.status;

  // ── Scheduled ──────────────────────────────────────────────────────────────
  if (status === 'STATUS_SCHEDULED') {
    const cityState = [matchup.venueCity, matchup.venueState].filter(Boolean).join(', ');
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
            {matchup.venue}
          </Text>
        ) : null}
        {cityState ? (
          <Text style={[styles.statusMeta, { color: theme.textMuted }]} numberOfLines={1}>
            {cityState}
          </Text>
        ) : null}
        <OverviewLink label="Game Preview" onPress={onPressGameDetail} theme={theme} />
      </View>
    );
  }

  // ── In Progress (+ paused-live banner overlay) ────────────────────────────
  const isDelayed = DELAY_STATUSES.has(status);
  if (
    status === 'STATUS_IN_PROGRESS' || status === 'STATUS_HALFTIME' ||
    isDelayed
  ) {
    const inProgressBlock =
      leagueSport === 'BaseballMlb' ? (
        <BaseballInProgress
          matchup={matchup}
          theme={theme}
          onPressGameDetail={onPressGameDetail}
          isDelayed={isDelayed}
        />
      ) : (
        <FootballInProgress
          matchup={matchup}
          theme={theme}
          onPressGameDetail={onPressGameDetail}
          isDelayed={isDelayed}
        />
      );

    // Delay status (SUSPENDED / DELAYED / RAIN_DELAY) is now displayed
    // inside the in-progress block itself — the LIVE slot is replaced
    // with the status description, styled as a muted static indicator.
    // The earlier delayBanner above the block led to contradictory
    // rendering (SUSPENDED above + pulsing LIVE below). Web mirrors
    // this in GameStatus.jsx.
    return inProgressBlock;
  }

  // ── Final ──────────────────────────────────────────────────────────────────
  if (status === 'STATUS_FINAL') {
    const awayScore = matchup.awayScore ?? 0;
    const homeScore = matchup.homeScore ?? 0;

    // SU quick-scan: a checkmark on the OUTSIDE of the winning team's
    // short (left of away or right of home). Skipped for ATS / O/U
    // leagues — those render their own indicator on a row below.
    // Skipped pre-enrichment (winnerFranchiseSeasonId null) and on a
    // true tie. Unknown / null pickType defaults to SU treatment so
    // callers that don't yet thread the prop through don't regress.
    const isSU = !pickType || pickType === 'StraightUp';
    const awayWonSU =
      isSU &&
      matchup.winnerFranchiseSeasonId != null &&
      matchup.winnerFranchiseSeasonId === matchup.awayFranchiseSeasonId;
    const homeWonSU =
      isSU &&
      matchup.winnerFranchiseSeasonId != null &&
      matchup.winnerFranchiseSeasonId === matchup.homeFranchiseSeasonId;

    return (
      <View style={styles.statusSection}>
        <Text style={[styles.statusLabel, { color: theme.textMuted }]}>FINAL</Text>
        <View style={styles.finalScoreRow}>
          {awayWonSU ? (
            <Text style={[styles.suCheck, { color: '#16A34A' }]}>✓</Text>
          ) : null}
          <Text style={[styles.scoreText, { color: theme.text }]}>
            {matchup.awayShort} {awayScore} – {homeScore} {matchup.homeShort}
          </Text>
          {homeWonSU ? (
            <Text style={[styles.suCheck, { color: '#16A34A' }]}>✓</Text>
          ) : null}
        </View>
        <FinalScoreResult
          pickType={pickType}
          awayFranchiseSeasonId={matchup.awayFranchiseSeasonId}
          homeFranchiseSeasonId={matchup.homeFranchiseSeasonId}
          awayShort={matchup.awayShort}
          homeShort={matchup.homeShort}
          winnerFranchiseSeasonId={matchup.winnerFranchiseSeasonId}
          spreadWinnerFranchiseSeasonId={matchup.spreadWinnerFranchiseSeasonId}
          overUnderResult={matchup.overUnderResult as string | number | null | undefined}
          overUnderCurrent={matchup.overUnderCurrent}
        />
        <OverviewLink label="Box Score" onPress={onPressGameDetail} theme={theme} />
      </View>
    );
  }

  // ── Terminal (Postponed / Canceled) ───────────────────────────────────────
  // Won't be played on the displayed time. Visually identical between the
  // two states — statusDescription provides the label.
  if (TERMINAL_STATUSES.has(status)) {
    const cityState = [matchup.venueCity, matchup.venueState].filter(Boolean).join(', ');
    return (
      <View style={styles.statusSection}>
        <Text style={[styles.statusLabel, { color: theme.tint }]}>
          {(matchup.statusDescription ?? matchup.status).toUpperCase()}
        </Text>
        <Text style={[styles.statusTime, styles.struckThrough, { color: theme.textMuted }]}>
          {formatToUserTime(matchup.startDateUtc, userTz)}
        </Text>
        {matchup.venue ? (
          <Text style={[styles.statusMeta, { color: theme.textMuted }]} numberOfLines={1}>
            {matchup.venue}
          </Text>
        ) : null}
        {cityState ? (
          <Text style={[styles.statusMeta, { color: theme.textMuted }]} numberOfLines={1}>
            {cityState}
          </Text>
        ) : null}
      </View>
    );
  }

  // ── Other (defensive — unrecognized status string) ────────────────────────
  return (
    <View style={styles.statusSection}>
      <Text style={[styles.statusLabel, { color: theme.error }]}>
        {matchup.statusDescription ?? matchup.status}
      </Text>
    </View>
  );
}

// ─── Overview link ───────────────────────────────────────────────────────────
//
// Bottom-of-status-block affordance for "tap to open the Contest Overview".
// Replaces the older pattern of wrapping the whole status block in a
// TouchableOpacity (which gave no visual hint that it was tappable).
//
// Per-state labels live at the call sites:
//   Scheduled  → "Game Preview"
//   InProgress → "Live Box Score"
//   Final      → "Box Score"

export function OverviewLink({
  label,
  onPress,
  theme,
  align = 'center',
}: {
  label: string;
  onPress?: () => void;
  theme: Theme;
  align?: 'center' | 'flex-start';
}) {
  if (!onPress) return null;
  return (
    <TouchableOpacity
      onPress={onPress}
      activeOpacity={0.7}
      style={[styles.overviewLink, { alignSelf: align }]}
      hitSlop={8}
    >
      <Text style={[styles.overviewLinkText, { color: theme.tint }]}>
        {label} ›
      </Text>
    </TouchableOpacity>
  );
}

// ─── InProgress sub-components ───────────────────────────────────────────────

type Theme = ReturnType<typeof getTheme>;

function FootballInProgress({
  matchup,
  theme,
  onPressGameDetail,
  // When status is SUSPENDED / DELAYED / RAIN_DELAY, the LIVE slot is
  // replaced with the delay status text and the red pulse dot is
  // dropped — the game isn't actively in play, so the red-pulse cue
  // would be misleading. Mirrors web's isDelayed prop.
  isDelayed = false,
}: {
  matchup: Matchup;
  theme: Theme;
  onPressGameDetail?: () => void;
  isDelayed?: boolean;
}) {
  const delayLabel = (
    matchup.statusDescription ?? matchup.status ?? 'PAUSED'
  ).toUpperCase();
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
        {isDelayed ? (
          <Text style={[styles.delayText, { color: theme.textMuted }]}>
            {delayLabel}
          </Text>
        ) : (
          <>
            <View style={styles.liveDot} />
            <Text style={styles.liveText}>LIVE</Text>
          </>
        )}
        {/* Period + clock renders in both branches: for a delayed game
            it's the meaningful "we paused at Q2 5:32" context. Mirrors
            web's FootballGameStatusInProgress where the .game-clock
            span is rendered outside the LIVE/delay-indicator ternary. */}
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

      <OverviewLink label="Live Box Score" onPress={onPressGameDetail} theme={theme} />
    </View>
  );
}

function BaseballInProgress({
  matchup,
  theme,
  onPressGameDetail,
  // See FootballInProgress isDelayed comment — same semantics,
  // mirrors the web isDelayed prop.
  isDelayed = false,
}: {
  matchup: Matchup;
  theme: Theme;
  onPressGameDetail?: () => void;
  isDelayed?: boolean;
}) {
  const delayLabel = (
    matchup.statusDescription ?? matchup.status ?? 'PAUSED'
  ).toUpperCase();
  const awayScore = matchup.awayScore ?? 0;
  const homeScore = matchup.homeScore ?? 0;
  // halfInning "Top" → away batting (gets the ⚾); "Bottom" → home batting.
  const half = (matchup.halfInning ?? '').toLowerCase();
  const awayIsBatting = half === 'top';
  const homeIsBatting = half === 'bottom';

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
        {isDelayed ? (
          <Text style={[styles.delayText, { color: theme.textMuted }]}>
            {delayLabel}
          </Text>
        ) : (
          <>
            <View style={styles.liveDot} />
            <Text style={styles.liveText}>LIVE</Text>
          </>
        )}
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

      <OverviewLink label="Live Box Score" onPress={onPressGameDetail} theme={theme} />
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
    fontSize: 14,
    fontWeight: '600',
  },
  statusMeta: {
    fontSize: 13,
  },
  statusLabel: {
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  struckThrough: {
    textDecorationLine: 'line-through',
    opacity: 0.7,
  },
  // Overview link — shared affordance for "tap to open the Contest Overview",
  // sized to be obviously a link without competing visually with the status
  // info above it. Color comes from theme.tint at the call site.
  overviewLink: {
    marginTop: 4,
  },
  overviewLinkText: {
    fontSize: 12,
    fontWeight: '600',
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
  // Same slot as liveText — rendered when status is SUSPENDED /
  // DELAYED / RAIN_DELAY. Color is applied at the call site from
  // theme.textMuted; no animation. Distinguishes paused-but-not-final
  // state from active live play.
  delayText: {
    fontSize: 12,
    fontWeight: '800',
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
  // STATUS_FINAL row: flex container for the score text plus the
  // optional SU checkmark that hugs the outside of the winning team's
  // short. Gap supplies breathing room between the score and the check
  // without text concatenation.
  finalScoreRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  suCheck: {
    fontSize: 15,
    fontWeight: '800',
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
