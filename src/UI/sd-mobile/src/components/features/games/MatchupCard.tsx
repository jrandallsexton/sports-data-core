import React, { useMemo, useState } from 'react';
import { View, TouchableOpacity, StyleSheet, Image } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Colors, getTheme } from '@/constants/Colors';
import type { Matchup, UserPick, PickChoice, PreviewResponse, TeamComparisonData, PickType } from '@/src/types/models';
import { matchupsApi } from '@/src/services/api/matchupsApi';
import { teamCardApi } from '@/src/services/api/teamCardApi';
import { useContestUpdate } from '@/src/stores/contestUpdatesStore';
import { useCurrentUser } from '@/src/hooks/useStandings';
import { useTeamFinalizedGames } from '@/src/hooks/useTeamFinalizedGames';
import { resolveSportLeague } from '@/src/utils/sportLinks';
import { InsightModal } from './InsightModal';
import { StatsComparisonModal } from './StatsComparisonModal';
import { GameStatus } from './GameStatus';
import { MiniSchedule } from './MiniSchedule';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function spreadLabel(spread: number | null | undefined): string {
  if (spread == null || spread === 0) return 'PK';
  return spread > 0 ? `+${spread}` : `${spread}`;
}

/** Returns { symbol, color } for spread movement, or null if no movement. */
function spreadArrow(current: number | null | undefined, open: number | null | undefined): { symbol: string; color: string } | null {
  if (current == null || open == null || !isFinite(current) || !isFinite(open) || current === open) return null;
  const absCurrent = Math.abs(current);
  const absOpen = Math.abs(open);
  if (absCurrent < absOpen) return { symbol: '▼', color: '#00c853' };
  if (absCurrent > absOpen) return { symbol: '▲', color: '#ff1744' };
  // Sign flipped (e.g. +1 → -1): favorite changed, abs values equal
  if (Math.sign(current) !== Math.sign(open)) {
    return current > open ? { symbol: '▲', color: '#ff1744' } : { symbol: '▼', color: '#00c853' };
  }
  return null;
}

/** Returns { symbol, color } for O/U movement, or null if no movement. */
function ouArrow(current: number | null | undefined, open: number | null | undefined): { symbol: string; color: string } | null {
  if (current == null || open == null || !isFinite(current) || !isFinite(open) || current === open) return null;
  if (current > open) return { symbol: '▲', color: '#ff1744' };
  if (current < open) return { symbol: '▼', color: '#00c853' };
  return null;
}

function formatRecord(wins?: number, losses?: number, confWins?: number, confLosses?: number): string {
  if (wins == null || losses == null) return '';
  let s = `${wins}-${losses}`;
  if (confWins != null && confLosses != null) s += ` (${confWins}-${confLosses})`;
  return s;
}

/** Returns true when picks should be locked (5 min before kickoff, or game started/finished).
 *  Canonical status set mirrors GameStatus.tsx: live, paused-live, and
 *  terminal-final variants all lock. The prior version lowercased the
 *  status and matched against bare 'final' / 'inprogress' which never
 *  matched the wire's STATUS_* shape — picks were only ever locked via
 *  the time-based fallback. */
const LOCKED_STATUSES = new Set([
  'STATUS_IN_PROGRESS',
  'STATUS_HALFTIME',
  'STATUS_FINAL',
  'STATUS_DELAYED',
  'STATUS_RAIN_DELAY',
  'STATUS_SUSPENDED',
]);

function isPickLocked(matchup: Matchup): boolean {
  if (LOCKED_STATUSES.has(matchup.status)) {
    return true;
  }
  const kickoff = new Date(matchup.startDateUtc).getTime();
  const now = Date.now();
  return now >= kickoff - 5 * 60 * 1000; // 5 minutes before kickoff
}

// ─── Team row ─────────────────────────────────────────────────────────────────

function TeamRow({
  matchup,
  side,
  isWinning,
  isPicked,
  isPickCorrect,
  isFinal,
  isScheduleOpen,
  onToggleSchedule,
  canShowSchedule,
}: {
  matchup: Matchup;
  side: 'home' | 'away';
  isWinning: boolean;
  isPicked: boolean;
  isPickCorrect: boolean | null;
  isFinal: boolean;
  isScheduleOpen: boolean;
  onToggleSchedule: () => void;
  canShowSchedule: boolean;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const isHome = side === 'home';
  const name = isHome ? matchup.home : matchup.away;
  const abbr = isHome ? matchup.homeShort : matchup.awayShort;
  // Dark-mode logo swap: some teams have a *Dark variant for use against
  // a dark surface (e.g. dark-on-white wordmarks that disappear against
  // theme.card in dark mode). Falls back to the default variant when no
  // dark version exists. Mirrors web's pattern in
  // sd-ui/src/components/matchups/MatchupCard.jsx.
  const logoUriLight = isHome ? matchup.homeLogoUri : matchup.awayLogoUri;
  const logoUriDark = isHome ? matchup.homeLogoUriDark : matchup.awayLogoUriDark;
  const logoUrl = scheme === 'dark' ? (logoUriDark ?? logoUriLight) : logoUriLight;
  const rank = isHome ? matchup.homeRank : matchup.awayRank;
  const score = isHome ? matchup.homeScore : matchup.awayScore;
  const wins = isHome ? matchup.homeWins : matchup.awayWins;
  const losses = isHome ? matchup.homeLosses : matchup.awayLosses;
  const confWins = isHome ? matchup.homeConferenceWins : matchup.awayConferenceWins;
  const confLosses = isHome ? matchup.homeConferenceLosses : matchup.awayConferenceLosses;
  const probablePitcher = isHome ? matchup.homeProbablePitcher : matchup.awayProbablePitcher;

  // On a finalized tie, neither side has isWinning true (parent uses
  // strict >); without the tie branch both team names would mute.
  const isTie =
    matchup.awayScore != null &&
    matchup.homeScore != null &&
    matchup.awayScore === matchup.homeScore;
  const isActive = !isFinal || isWinning || isTie;
  const record = formatRecord(wins, losses, confWins, confLosses);

  // Pick indicator styling — uses the same theme.pickCorrect /
  // theme.pickIncorrect tokens as PickButton and the card border, so all
  // three result cues stay in lockstep across themes and match web's
  // --pick-correct / --pick-incorrect palette.
  let pickIndicatorColor: string | null = null;
  if (isPicked && isFinal) {
    pickIndicatorColor = isPickCorrect ? theme.pickCorrect : theme.pickIncorrect;
  } else if (isPicked) {
    pickIndicatorColor = theme.tint;
  }

  return (
    // Background lives at the call site so the team row gets theme.card,
    // visually offset from the outer card (which uses theme.background).
    // Mirrors sd-ui's `.team-row { background: var(--bg-card); }` against
    // `.matchup-card { background: var(--bg-primary); }`.
    <View style={[styles.teamRow, { backgroundColor: theme.card }]}>
      {/* Logo */}
      <View style={styles.logoBox}>
        {logoUrl ? (
          <Image source={{ uri: logoUrl }} style={styles.logo} resizeMode="contain" />
        ) : (
          <View style={[styles.logoPlaceholder, { backgroundColor: theme.border }]}>
            <Text style={{ color: theme.textMuted, fontSize: 11, fontWeight: '700' }}>
              {abbr.slice(0, 3)}
            </Text>
          </View>
        )}
      </View>

      {/* Name + record */}
      <View style={styles.teamInfo}>
        <Text
          style={[styles.teamName, { color: isActive ? theme.text : theme.textMuted }]}
          numberOfLines={1}
        >
          {rank != null ? <Text style={[styles.rankText, { color: theme.tint }]}>#{rank} </Text> : null}
          {name}
        </Text>
        <View style={styles.recordRow}>
          {record !== '' && (
            <Text style={[styles.recordText, { color: theme.textMuted }]}>{record}</Text>
          )}
          {canShowSchedule && (
            <TouchableOpacity
              onPress={onToggleSchedule}
              hitSlop={{ top: 8, right: 8, bottom: 8, left: 8 }}
              accessibilityLabel={isScheduleOpen ? 'Hide recent games' : 'Show recent games'}
            >
              <Ionicons
                name={isScheduleOpen ? 'chevron-up' : 'chevron-down'}
                size={16}
                color={theme.tint}
              />
            </TouchableOpacity>
          )}
        </View>
        {probablePitcher?.displayName ? (
          <View style={styles.probablePitcherRow}>
            {probablePitcher.headshotUrl ? (
              <Image
                source={{ uri: probablePitcher.headshotUrl }}
                style={styles.probablePitcherHeadshot}
                accessibilityIgnoresInvertColors
              />
            ) : null}
            <Text
              style={[styles.probablePitcherName, { color: theme.textMuted }]}
              numberOfLines={1}
            >
              {probablePitcher.displayName}
            </Text>
          </View>
        ) : null}
      </View>

      {/* Score + pick indicator — only render when there's content to show.
          Pre-game with no pick, the empty box was reserving minWidth + the
          parent row's gap, which squeezed the team name in the compact
          layout and forced truncation ("Cleveland Guar..."). */}
      {(pickIndicatorColor || score != null) && (
        <View style={styles.scoreBox}>
          {pickIndicatorColor && (
            <Text style={[styles.pickIndicator, { color: pickIndicatorColor }]}>
              {isPicked && isFinal ? (isPickCorrect ? '✓' : '✗') : '▶'}
            </Text>
          )}
          {score != null && (
            <Text
              style={[
                styles.score,
                { color: isWinning && isFinal ? theme.tint : theme.text },
                isWinning && isFinal && styles.scoreWinner,
              ]}
            >
              {score}
            </Text>
          )}
        </View>
      )}
    </View>
  );
}

// ─── Spread & O/U row ─────────────────────────────────────────────────────────

function OddsRow({ matchup }: { matchup: Matchup }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const spread = matchup.spreadCurrent;
  const spreadOpen = matchup.spreadOpen ?? null;
  const ou = matchup.overUnderCurrent;
  const ouOpen = matchup.overUnderOpen ?? null;

  const hasSpread = spread != null;
  const hasOu = ou != null && ou !== 0;

  if (!hasSpread && !hasOu) return null;

  const sArrow = spreadArrow(spread, spreadOpen);
  const oArrow = ouArrow(ou, ouOpen);
  const spreadVal = hasSpread ? spreadLabel(spread) : 'Off';
  const ouVal = hasOu ? `${ou}` : 'Off';
  const showSpreadOpen = hasSpread && spreadOpen != null && spreadOpen !== spread;
  const showOuOpen = hasOu && ouOpen != null && ouOpen !== ou;

  return (
    <View style={[styles.oddsRow, { borderColor: theme.separator }]}>
      {/* Spread */}
      {hasSpread && (
        <View style={styles.oddsInline}>
          <Text style={[styles.oddsLabel, { color: theme.textMuted }]}>Spread </Text>
          {sArrow && (
            <Text style={[styles.oddsArrow, { color: sArrow.color }]}>{sArrow.symbol}</Text>
          )}
          <Text style={[styles.oddsValue, { color: theme.tint }]}>{spreadVal}</Text>
          {showSpreadOpen && (
            <Text style={[styles.oddsOpen, { color: theme.textMuted }]}>
              {' '}({spreadOpen! > 0 ? `+${spreadOpen}` : spreadOpen})
            </Text>
          )}
        </View>
      )}

      {/* Separator */}
      {hasSpread && hasOu && (
        <Text style={[styles.oddsSep, { color: theme.textMuted }]}> | </Text>
      )}

      {/* O/U */}
      {hasOu && (
        <View style={styles.oddsInline}>
          <Text style={[styles.oddsLabel, { color: theme.textMuted }]}>O/U </Text>
          {oArrow && (
            <Text style={[styles.oddsArrow, { color: oArrow.color }]}>{oArrow.symbol}</Text>
          )}
          <Text style={[styles.oddsValue, { color: theme.tint }]}>{ouVal}</Text>
          {showOuOpen && (
            <Text style={[styles.oddsOpen, { color: theme.textMuted }]}>
              {' '}({ouOpen})
            </Text>
          )}
        </View>
      )}
    </View>
  );
}

// StatusSection extracted → see GameStatus.tsx

// ─── PickButton (mirrors web PickButton component) ───────────────────────────
//
// States (parallel to web CSS classes):
//   selected + correct   → green border/bg, ✓ icon
//   selected + incorrect → red border/bg,   ✗ icon
//   selected + pending   → navy border/bg,  ✓ icon
//   not selected + locked → lock icon in button
//   not selected + open  → neutral, pressable

function PickButton({
  teamShort,
  isSelected,
  pickResult,
  isLocked,
  onPress,
}: {
  teamShort: string;
  isSelected: boolean;
  pickResult: 'correct' | 'incorrect' | null;
  isLocked: boolean;
  onPress: () => void;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  let borderColor = theme.border;
  let bgColor = theme.background;
  let teamColor = theme.text;

  // Web parity: scored picks render as solid green/red buttons with
  // white text (mirrors .pick-button.result-correct / .result-incorrect
  // in sd-ui/src/components/matchups/MatchupCard.css). theme.textOnAccent
  // is NOT used here — it's #111 in dark mode (intended for the bright
  // cyan accent) and would render as dark-on-dark-green / dark-on-dark-red
  // for the pick result colors. White holds up against both light-mode
  // (#1b5e20 / #b71c1c) and dark-mode (#28a745 / #dc3545) palettes.
  const ON_RESULT_FG = '#ffffff';
  if (isSelected && pickResult === 'correct') {
    borderColor = theme.pickCorrect; bgColor = theme.pickCorrect; teamColor = ON_RESULT_FG;
  } else if (isSelected && pickResult === 'incorrect') {
    borderColor = theme.pickIncorrect; bgColor = theme.pickIncorrect; teamColor = ON_RESULT_FG;
  } else if (isSelected) {
    borderColor = Colors.brand.navy; bgColor = '#EEF2FF'; teamColor = Colors.brand.navy;
  }

  return (
    <TouchableOpacity
      style={[styles.pickBtn, { borderColor, backgroundColor: bgColor }]}
      onPress={onPress}
      disabled={isLocked}
      activeOpacity={isLocked ? 1 : 0.7}
    >
      {/* ✓ when selected and not incorrect — white on the solid green
          correct-bg, navy on the pale pending-bg. White is hardcoded
          (not theme.textOnAccent) so it stays legible against the dark
          green / red pickCorrect-pickIncorrect palette in both themes. */}
      {isSelected && pickResult !== 'incorrect' && (
        <Text style={[styles.pickIcon, { color: pickResult === 'correct' ? '#ffffff' : Colors.brand.navy }]}>✓</Text>
      )}
      {/* ✗ when selected + incorrect — white on the solid red bg. */}
      {isSelected && pickResult === 'incorrect' && (
        <Text style={[styles.pickIcon, { color: '#ffffff' }]}>✗</Text>
      )}
      {/* 🔒 when not selected + locked + no result (missed / pre-game locked) */}
      {!isSelected && isLocked && pickResult === null && (
        <Text style={[styles.pickIcon, { color: theme.textMuted }]}>🔒</Text>
      )}
      <Text style={[styles.pickBtnTeam, { color: teamColor }]} numberOfLines={1}>
        {teamShort}
      </Text>
    </TouchableOpacity>
  );
}

// ─── Pick buttons row ─────────────────────────────────────────────────────────

function PickButtons({
  matchup,
  pickedFranchiseId,
  isPickCorrect,
  isFinal,
  locked,
  onPick,
  onOpenStats,
  onOpenPreview,
}: {
  matchup: Matchup;
  pickedFranchiseId: string | null;
  isPickCorrect: boolean | null;
  isFinal: boolean;
  locked: boolean;
  onPick: (choice: PickChoice, franchiseSeasonId: string) => void;
  onOpenStats?: () => void;
  onOpenPreview?: () => void;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const pickedHome = pickedFranchiseId === matchup.homeFranchiseSeasonId;
  const pickedAway = pickedFranchiseId === matchup.awayFranchiseSeasonId;

  // Derive pickResult string to pass to each PickButton
  const pickResultStr: 'correct' | 'incorrect' | null = isFinal
    ? isPickCorrect === true ? 'correct' : isPickCorrect === false ? 'incorrect' : null
    : null;

  return (
    <View style={[styles.pickRow, { borderColor: theme.separator }]}>
      <PickButton
        teamShort={matchup.awayShort}
        isSelected={pickedAway}
        pickResult={pickedAway ? pickResultStr : null}
        isLocked={locked}
        onPress={() => onPick('away', matchup.awayFranchiseSeasonId)}
      />
      <TouchableOpacity
        style={[styles.actionBtn, { backgroundColor: theme.separator }]}
        onPress={onOpenStats}
        activeOpacity={0.7}
        hitSlop={6}
      >
        <Text style={styles.actionBtnIcon}>📋</Text>
      </TouchableOpacity>
      <TouchableOpacity
        style={[
          styles.actionBtn,
          { backgroundColor: theme.separator },
          !matchup.isPreviewAvailable && styles.actionBtnDisabled,
        ]}
        onPress={onOpenPreview}
        disabled={!matchup.isPreviewAvailable}
        activeOpacity={0.7}
        hitSlop={6}
      >
        <Text style={[styles.actionBtnIcon, !matchup.isPreviewAvailable && { opacity: 0.4 }]}>
          📈
        </Text>
      </TouchableOpacity>
      <PickButton
        teamShort={matchup.homeShort}
        isSelected={pickedHome}
        pickResult={pickedHome ? pickResultStr : null}
        isLocked={locked}
        onPress={() => onPick('home', matchup.homeFranchiseSeasonId)}
      />
    </View>
  );
}

// ─── Main component ───────────────────────────────────────────────────────────

export interface MatchupCardProps {
  matchup: Matchup;
  pick?: UserPick | null;
  /** Tap on the spread/result area → opens the contest overview. */
  onPress?: () => void;
  /** Tap on a team row → opens that team's page. */
  onPressTeam?: (side: 'home' | 'away') => void;
  onPick?: (matchup: Matchup, choice: PickChoice, franchiseSeasonId: string) => void;
  /** Season year used for team stats API calls. Defaults to the game start year. */
  seasonYear?: number;
  /**
   * Backend Sport enum name ("FootballNcaa" | "FootballNfl" | "BaseballMlb").
   * Threaded through to GameStatus for sport-aware InProgress rendering.
   * Resolved from LeagueMatchupsResponse.sport at the screen level.
   */
  leagueSport?: string | null;
  /**
   * SeasonWeek.EndDate of the displayed week (ISO 8601), surfaced via
   * LeagueWeekMatchupsDto.asOfDate. Passed into MiniSchedule's fetch as an
   * inclusive FinalizedUtc upper bound so the historical-pick-review case
   * doesn't show results from games the picker couldn't yet have seen. Date
   * filter (not week number) so MLB same-week games and football post-season
   * Week-1 reuse both behave correctly.
   */
  leagueAsOfDate?: string | null;
  /**
   * League pick mode (StraightUp / AgainstTheSpread / OverUnder), surfaced
   * via LeagueWeekMatchupsDto.pickType. Threaded to GameStatus for the
   * STATUS_FINAL quick-scan indicator (inline ✓ for SU, "covered" / Over /
   * Under / Push row for ATS / O/U). Optional — falls back to SU visuals.
   */
  pickType?: PickType | null;
}

export function MatchupCard({ matchup, pick, onPress, onPressTeam, onPick, seasonYear, leagueSport, leagueAsOfDate, pickType }: MatchupCardProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  // Live-update subscription. Only re-renders this card when its own
  // contestId's record changes — see contestUpdatesStore selector design.
  const live = useContestUpdate(matchup.contestId);

  // Current user — only the isReadOnly flag is consumed here, to lock
  // picks for read-only viewers regardless of matchup status. Shares a
  // TanStack Query cache with the screen-level useCurrentUser call, so
  // this doesn't trigger an extra network request per card.
  const { data: me } = useCurrentUser();

  // Merge live data over the static REST payload via nullish fallback so
  // a partial future-update handler can't silently undefine a previously
  // populated field. Mirrors the web pattern in AdminBaseballPage.jsx
  // and AdminFootballPage.jsx.
  const enrichedMatchup = useMemo<Matchup>(() => {
    if (!live) return matchup;
    return {
      ...matchup,
      status: live.status ?? matchup.status,
      awayScore: live.awayScore ?? matchup.awayScore,
      homeScore: live.homeScore ?? matchup.homeScore,
      // Football live fields
      period: live.period ?? matchup.period,
      clock: live.clock ?? matchup.clock,
      possessionFranchiseSeasonId:
        live.possessionFranchiseSeasonId ?? matchup.possessionFranchiseSeasonId,
      isScoringPlay: live.isScoringPlay ?? matchup.isScoringPlay,
      ballOnYardLine: live.ballOnYardLine ?? matchup.ballOnYardLine,
      // Baseball live fields
      inning: live.inning ?? matchup.inning,
      halfInning: live.halfInning ?? matchup.halfInning,
      balls: live.balls ?? matchup.balls,
      strikes: live.strikes ?? matchup.strikes,
      outs: live.outs ?? matchup.outs,
      runnerOnFirst: live.runnerOnFirst ?? matchup.runnerOnFirst,
      runnerOnSecond: live.runnerOnSecond ?? matchup.runnerOnSecond,
      runnerOnThird: live.runnerOnThird ?? matchup.runnerOnThird,
      atBatAthleteSeasonId: live.atBatAthleteSeasonId ?? matchup.atBatAthleteSeasonId,
      atBatShortName: live.atBatShortName ?? matchup.atBatShortName,
      atBatPositionAbbreviation:
        live.atBatPositionAbbreviation ?? matchup.atBatPositionAbbreviation,
      atBatHeadshotUrl: live.atBatHeadshotUrl ?? matchup.atBatHeadshotUrl,
      pitchingAthleteSeasonId:
        live.pitchingAthleteSeasonId ?? matchup.pitchingAthleteSeasonId,
      pitchingShortName: live.pitchingShortName ?? matchup.pitchingShortName,
      pitchingPositionAbbreviation:
        live.pitchingPositionAbbreviation ?? matchup.pitchingPositionAbbreviation,
      pitchingHeadshotUrl: live.pitchingHeadshotUrl ?? matchup.pitchingHeadshotUrl,
      lastPlayId: live.lastPlayId ?? matchup.lastPlayId,
      lastPlayDescription: live.lastPlayDescription ?? matchup.lastPlayDescription,
    };
  }, [matchup, live]);

  // Use the canonical ESPN status name directly. The prior version
  // lowercased and compared against bare 'final' / 'completed', which
  // never matched the wire's STATUS_FINAL shape — so isFinal was always
  // false and the entire scored-card visual pipeline (PickButton
  // green/red, team-row indicator ✓/✗, card border tint, winning-team
  // score color) silently no-op'd.
  const isFinal = enrichedMatchup.status === 'STATUS_FINAL';

  // Optimistic local pick — shows selection instantly before server confirms
  const [optimisticFranchiseId, setOptimisticFranchiseId] = useState<string | null>(null);

  // ── Stats modal state ──────────────────────────────────────────────────────
  const [showStats, setShowStats] = useState(false);
  const [statsData, setStatsData] = useState<TeamComparisonData | null>(null);
  const [statsLoading, setStatsLoading] = useState(false);

  // ── Preview / insight modal state ──────────────────────────────────────────
  const [showPreview, setShowPreview] = useState(false);
  const [previewData, setPreviewData] = useState<PreviewResponse | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);

  // ── Mini-schedule state ────────────────────────────────────────────────────
  // Each team's schedule expands independently. Lazy-fetched via useTeamCard
  // with an `enabled` gate so cards in the feed don't burn requests for
  // schedules the user never opens.
  const [showAwaySchedule, setShowAwaySchedule] = useState(false);
  const [showHomeSchedule, setShowHomeSchedule] = useState(false);

  const year = seasonYear ?? new Date(matchup.startDateUtc).getFullYear();
  const sportLeague = resolveSportLeague(leagueSport);

  // Only fetch when the sport enum resolves — otherwise we'd issue a
  // football/ncaa request for a slug that lives in a different sport
  // (resolveSportLeague returns null for unmapped/unknown enums by design).
  const awayFinalizedGames = useTeamFinalizedGames(
    matchup.awaySlug ?? null,
    year,
    sportLeague?.sport ?? 'football',
    sportLeague?.league ?? 'ncaa',
    showAwaySchedule && !!sportLeague,
    leagueAsOfDate ?? null,
  );
  const homeFinalizedGames = useTeamFinalizedGames(
    matchup.homeSlug ?? null,
    year,
    sportLeague?.sport ?? 'football',
    sportLeague?.league ?? 'ncaa',
    showHomeSchedule && !!sportLeague,
    leagueAsOfDate ?? null,
  );

  const handleOpenStats = async () => {
    setShowStats(true);

    // Resolve the logo URI against the live scheme. Same fallback rule as
    // TeamRow. Computed at the top of the handler so the cache check below
    // can compare against current values and refresh if the user toggled
    // themes between opens.
    const awayLogoUri =
      scheme === 'dark' ? (matchup.awayLogoUriDark ?? matchup.awayLogoUri) : matchup.awayLogoUri;
    const homeLogoUri =
      scheme === 'dark' ? (matchup.homeLogoUriDark ?? matchup.homeLogoUri) : matchup.homeLogoUri;

    // Stats / metrics are cached across opens, but the resolved logo URIs
    // depend on the active theme. If the user toggled themes between
    // opens, refresh just the logoUri fields on the existing cache —
    // don't re-fetch stats.
    if (statsData) {
      if (
        statsData.teamA.logoUri !== awayLogoUri ||
        statsData.teamB.logoUri !== homeLogoUri
      ) {
        setStatsData({
          teamA: { ...statsData.teamA, logoUri: awayLogoUri },
          teamB: { ...statsData.teamB, logoUri: homeLogoUri },
        });
      }
      return;
    }

    setStatsLoading(true);
    try {
      const [awayStats, homeStats, awayMetrics, homeMetrics] = await Promise.all([
        teamCardApi.getStatistics(matchup.awaySlug, year, matchup.awayFranchiseSeasonId),
        teamCardApi.getStatistics(matchup.homeSlug, year, matchup.homeFranchiseSeasonId),
        teamCardApi.getMetrics(matchup.awaySlug, year, matchup.awayFranchiseSeasonId),
        teamCardApi.getMetrics(matchup.homeSlug, year, matchup.homeFranchiseSeasonId),
      ]);
      setStatsData({
        teamA: { name: matchup.away, logoUri: awayLogoUri, stats: awayStats, metrics: awayMetrics },
        teamB: { name: matchup.home, logoUri: homeLogoUri, stats: homeStats, metrics: homeMetrics },
      });
    } catch (err) {
      console.warn('[MatchupCard] stats fetch error', err);
    } finally {
      setStatsLoading(false);
    }
  };

  const handleOpenPreview = async () => {
    setShowPreview(true);
    if (previewData) return; // already loaded
    setPreviewLoading(true);
    try {
      const res = await matchupsApi.getPreview(matchup.contestId);
      setPreviewData(res.data);
    } catch (err) {
      console.warn('[MatchupCard] preview fetch error', err);
    } finally {
      setPreviewLoading(false);
    }
  };

  // Strict > on each side so a true tie (rare in NFL, impossible in MLB)
  // leaves BOTH sides' isWinning false — TeamRow's tie-aware isActive
  // then keeps both team names at full text color and skips the
  // winner-only tint / fontSize bump. Prior >= treated home as the
  // winner on a tie, and the away row received !homeIsWinning which
  // flipped the tie into "away wins" for downstream styling.
  const homeIsWinning =
    enrichedMatchup.homeScore != null &&
    enrichedMatchup.awayScore != null &&
    enrichedMatchup.homeScore > enrichedMatchup.awayScore;

  const awayIsWinning =
    enrichedMatchup.awayScore != null &&
    enrichedMatchup.homeScore != null &&
    enrichedMatchup.awayScore > enrichedMatchup.homeScore;

  // Use server pick if available, otherwise optimistic
  const effectiveFranchiseId = pick?.franchiseId ?? optimisticFranchiseId;

  const pickedHome = effectiveFranchiseId === matchup.homeFranchiseSeasonId;
  const pickedAway = effectiveFranchiseId === matchup.awayFranchiseSeasonId;
  const hasPick = pickedHome || pickedAway;

  // Pick result
  const isPickCorrect = isFinal && hasPick ? (pick?.isCorrect ?? null) : null;

  // Use enriched status — a live transition to InProgress must lock picks.
  // Also lock for read-only viewers (e.g. shared link accounts) so they
  // can't tap pick buttons regardless of game state. Mirrors the web
  // `usePickLocking` hook's `userDto.isReadOnly` consultation.
  const locked = isPickLocked(enrichedMatchup) || !!me?.isReadOnly;

  // Card border color based on pick result — same theme tokens as
  // PickButton bg and the team-row indicator, so all three result cues
  // share one palette (mirrors web .matchup-card.pick-correct /
  // .pick-incorrect / .pick-no-submission borders using --pick-correct
  // / --pick-incorrect).
  let cardBorderColor = theme.border;
  if (isFinal && hasPick) {
    if (isPickCorrect === true) cardBorderColor = theme.pickCorrect;
    else if (isPickCorrect === false) cardBorderColor = theme.pickIncorrect;
  } else if (isFinal && !hasPick) {
    cardBorderColor = theme.pickIncorrect; // missed pick
  }

  const handlePick = (choice: PickChoice, franchiseSeasonId: string) => {
    setOptimisticFranchiseId(franchiseSeasonId);
    onPick?.(matchup, choice, franchiseSeasonId);
  };

  return (
    <>
    <View
      style={[
        styles.card,
        // Outer card uses theme.background (mirrors web's --bg-primary on
        // .matchup-card). The inner team rows then carry theme.card and
        // visually pop as inset rows — same layered look the web app has.
        { backgroundColor: theme.background, borderColor: cardBorderColor },
        isFinal && hasPick && isPickCorrect === true && styles.cardCorrect,
        isFinal && (isPickCorrect === false || !hasPick) && styles.cardIncorrect,
      ]}
    >
      {/* Headline banner — theme-aware accent background + text-on-accent
          foreground match web's .matchup-headline { background-color:
          var(--accent); color: var(--text-on-accent); }. Light → white on
          blue, dark → dark on cyan. The marquee tag (bowl name, conf
          championship, series-leader summary) reads as a branded chip
          rather than a static navy bar. */}
      {matchup.headLine != null && matchup.headLine !== '' && (
        <View style={[styles.headline, { backgroundColor: theme.tint }]}>
          <Text
            style={[styles.headlineText, { color: theme.textOnAccent }]}
            numberOfLines={1}
          >
            {matchup.headLine}
          </Text>
        </View>
      )}

      {/* Away team — taps go to the team page, not the contest overview.
          MiniSchedule sits outside the touchable so taps within the schedule
          don't navigate to the team page. */}
      <View>
        <TouchableOpacity
          onPress={onPressTeam ? () => onPressTeam('away') : undefined}
          activeOpacity={onPressTeam ? 0.6 : 1}
          disabled={!onPressTeam}
        >
          <TeamRow
            matchup={enrichedMatchup}
            side="away"
            isWinning={awayIsWinning}
            isPicked={pickedAway}
            isPickCorrect={isPickCorrect}
            isFinal={isFinal}
            isScheduleOpen={showAwaySchedule}
            onToggleSchedule={() => setShowAwaySchedule((v) => !v)}
            canShowSchedule={!!sportLeague}
          />
        </TouchableOpacity>
        {showAwaySchedule && (
          <MiniSchedule
            schedule={awayFinalizedGames.data}
            seasonYear={year}
            leagueSport={leagueSport}
            loading={awayFinalizedGames.isLoading}
            error={awayFinalizedGames.isError ? 'Failed to load games' : null}
            teamName={matchup.away}
          />
        )}
      </View>

      {/* Home team */}
      <View>
        <TouchableOpacity
          onPress={onPressTeam ? () => onPressTeam('home') : undefined}
          activeOpacity={onPressTeam ? 0.6 : 1}
          disabled={!onPressTeam}
        >
          <TeamRow
            matchup={enrichedMatchup}
            side="home"
            isWinning={homeIsWinning}
            isPicked={pickedHome}
            isPickCorrect={isPickCorrect}
            isFinal={isFinal}
            isScheduleOpen={showHomeSchedule}
            onToggleSchedule={() => setShowHomeSchedule((v) => !v)}
            canShowSchedule={!!sportLeague}
          />
        </TouchableOpacity>
        {showHomeSchedule && (
          <MiniSchedule
            schedule={homeFinalizedGames.data}
            seasonYear={year}
            leagueSport={leagueSport}
            loading={homeFinalizedGames.isLoading}
            error={homeFinalizedGames.isError ? 'Failed to load games' : null}
            teamName={matchup.home}
          />
        )}
      </View>

      {/* Spread & O/U — tap goes to the contest overview. */}
      <TouchableOpacity
        onPress={onPress}
        activeOpacity={onPress ? 0.75 : 1}
        disabled={!onPress}
      >
        <OddsRow matchup={enrichedMatchup} />
      </TouchableOpacity>

      {/* Game status — time/score/live; GameStatus owns its own touchable
          and routes to the contest overview via onPressGameDetail.
          enriched data drives all status branches; leagueSport dispatches
          the InProgress UI between baseball and football. */}
      <GameStatus
        matchup={enrichedMatchup}
        leagueSport={leagueSport}
        onPressGameDetail={onPress}
        pickType={pickType}
      />

      {/* Inline pick buttons */}
      {onPick && (
        <PickButtons
          matchup={matchup}
          pickedFranchiseId={effectiveFranchiseId ?? null}
          isPickCorrect={isPickCorrect}
          isFinal={isFinal}
          locked={locked}
          onPick={handlePick}
          onOpenStats={handleOpenStats}
          onOpenPreview={handleOpenPreview}
        />
      )}
    </View>

    {/* Modals — rendered outside the card so they overlay in full screen */}
    <StatsComparisonModal
      visible={showStats}
      onClose={() => setShowStats(false)}
      matchup={matchup}
      comparison={statsData}
      isLoading={statsLoading}
    />
    <InsightModal
      visible={showPreview}
      onClose={() => setShowPreview(false)}
      matchup={matchup}
      preview={previewData}
      isLoading={previewLoading}
    />
    </>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  card: {
    borderRadius: 14,
    borderWidth: 1.5,
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.08,
    shadowRadius: 10,
    elevation: 3,
  },
  cardCorrect: {
    shadowColor: '#16A34A',
    shadowOpacity: 0.2,
  },
  cardIncorrect: {
    shadowColor: '#DC2626',
    shadowOpacity: 0.2,
  },

  // Headline — backgroundColor lives at the call site so it can pick
  // up theme.tint (accent). See web parity comment above the <View />.
  headline: {
    paddingVertical: 6,
    paddingHorizontal: 14,
  },
  headlineText: {
    // color lives at the call site so it picks up theme.textOnAccent.
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    textAlign: 'center',
  },

  // Team row — visual offset from the outer card. Mirrors sd-ui's .team-row:
  //   background-color: var(--bg-card); border-radius: 8px; margin-bottom: 0.5rem;
  // Background color is applied at the call site (theme.card) so the
  // tokens stay consistent across light/dark.
  teamRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 14,
    paddingVertical: 10,
    gap: 10,
    borderRadius: 8,
    marginBottom: 6,
  },
  logoBox: {
    width: 40,
    height: 40,
    alignItems: 'center',
    justifyContent: 'center',
  },
  logo: {
    width: 38,
    height: 38,
  },
  logoPlaceholder: {
    width: 38,
    height: 38,
    borderRadius: 19,
    alignItems: 'center',
    justifyContent: 'center',
  },
  teamInfo: {
    flex: 1,
    gap: 1,
  },
  teamName: {
    fontSize: 15,
    fontWeight: '600',
  },
  rankText: {
    fontWeight: '700',
  },
  recordRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  recordText: {
    fontSize: 13,
  },
  probablePitcherRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginTop: 2,
  },
  probablePitcherHeadshot: {
    width: 24,
    height: 24,
    borderRadius: 12,
  },
  probablePitcherName: {
    fontSize: 12,
    fontWeight: '500',
    flexShrink: 1,
  },
  scoreBox: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    minWidth: 36,
    justifyContent: 'flex-end',
  },
  score: {
    fontSize: 20,
    fontWeight: '700',
    minWidth: 28,
    textAlign: 'right',
  },
  scoreWinner: {
    fontSize: 22,
  },
  pickIndicator: {
    fontSize: 14,
    fontWeight: '800',
  },

  // Status styles live in GameStatus.tsx

  // Odds row
  oddsRow: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    borderTopWidth: StyleSheet.hairlineWidth,
    paddingVertical: 6,
    paddingHorizontal: 14,
    flexWrap: 'wrap',
  },
  oddsInline: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  oddsLabel: {
    fontSize: 12,
    fontWeight: '600',
    textTransform: 'uppercase',
    letterSpacing: 0.3,
  },
  oddsArrow: {
    fontSize: 12,
    fontWeight: '700',
    marginRight: 2,
  },
  oddsValue: {
    fontSize: 13,
    fontWeight: '700',
  },
  oddsOpen: {
    fontSize: 11,
  },
  oddsSep: {
    fontSize: 13,
    paddingHorizontal: 4,
  },

  // Pick buttons
  pickRow: {
    flexDirection: 'row',
    alignItems: 'center',
    borderTopWidth: StyleSheet.hairlineWidth,
    paddingVertical: 10,
    paddingHorizontal: 14,
    gap: 8,
    justifyContent: 'center',
  },
  pickBtn: {
    flex: 1,
    // Row layout matches the web's PickButton: icon and team short sit
    // side-by-side instead of stacking. Stacking was the source of the
    // disproportionately-tall mobile pick buttons vs web parity.
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1.5,
    borderRadius: 10,
    paddingVertical: 8,
    paddingHorizontal: 8,
    gap: 4,
  },
  pickIcon: {
    fontSize: 13,
    fontWeight: '800',
    lineHeight: 16,
  },
  pickBtnTeam: {
    fontSize: 14,
    fontWeight: '700',
  },
  pickVsBox: {
    width: 28,
    height: 28,
    borderRadius: 14,
    alignItems: 'center',
    justifyContent: 'center',
  },
  pickVsText: {
    fontSize: 12,
    fontWeight: '700',
  },
  actionBtn: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  actionBtnDisabled: {
    opacity: 0.5,
  },
  actionBtnIcon: {
    fontSize: 17,
  },
  pickLockedText: {
    fontSize: 12,
    fontWeight: '600',
    paddingVertical: 4,
  },
});
