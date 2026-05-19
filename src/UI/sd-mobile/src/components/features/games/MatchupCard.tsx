import React, { useMemo, useState } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Image } from 'react-native';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Colors, getTheme } from '@/constants/Colors';
import type { Matchup, UserPick, PickChoice, PreviewResponse, TeamComparisonData } from '@/src/types/models';
import { matchupsApi } from '@/src/services/api/matchupsApi';
import { teamCardApi } from '@/src/services/api/teamCardApi';
import { useContestUpdate } from '@/src/stores/contestUpdatesStore';
import { useCurrentUser } from '@/src/hooks/useStandings';
import { formatToUserTime } from '@/src/utils/timeUtils';
import { useUserTimeZone } from '@/src/hooks/useUserTimeZone';
import { InsightModal } from './InsightModal';
import { StatsComparisonModal } from './StatsComparisonModal';
import { GameStatus, OverviewLink } from './GameStatus';

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

/** Returns true when picks should be locked (5 min before kickoff, or game started/finished). */
function isPickLocked(matchup: Matchup): boolean {
  const status = matchup.status.toLowerCase();
  if (status === 'inprogress' || status === 'ongoing' || status === 'halftime' || status === 'final' || status === 'completed') {
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
}: {
  matchup: Matchup;
  side: 'home' | 'away';
  isWinning: boolean;
  isPicked: boolean;
  isPickCorrect: boolean | null;
  isFinal: boolean;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const isHome = side === 'home';
  const name = isHome ? matchup.home : matchup.away;
  const abbr = isHome ? matchup.homeShort : matchup.awayShort;
  const logoUrl = isHome ? matchup.homeLogoUri : matchup.awayLogoUri;
  const rank = isHome ? matchup.homeRank : matchup.awayRank;
  const score = isHome ? matchup.homeScore : matchup.awayScore;
  const wins = isHome ? matchup.homeWins : matchup.awayWins;
  const losses = isHome ? matchup.homeLosses : matchup.awayLosses;
  const confWins = isHome ? matchup.homeConferenceWins : matchup.awayConferenceWins;
  const confLosses = isHome ? matchup.homeConferenceLosses : matchup.awayConferenceLosses;
  const probablePitcher = isHome ? matchup.homeProbablePitcher : matchup.awayProbablePitcher;

  const isActive = !isFinal || isWinning;
  const record = formatRecord(wins, losses, confWins, confLosses);

  // Pick indicator styling
  let pickIndicatorColor: string | null = null;
  if (isPicked && isFinal) {
    pickIndicatorColor = isPickCorrect ? '#16A34A' : '#DC2626';
  } else if (isPicked) {
    pickIndicatorColor = theme.tint;
  }

  return (
    <View style={styles.teamRow}>
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
        {record !== '' && (
          <Text style={[styles.recordText, { color: theme.textMuted }]}>{record}</Text>
        )}
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

// ─── Scheduled meta (compact 2-column right side) ────────────────────────────
//
// Pre-game stack: spread (and O/U) above, then date/time, then broadcasts,
// then venue. Mirrors what GameStatus renders for Scheduled but stacked
// vertically in a narrow right column instead of centered full-width.

function ScheduledMeta({
  matchup,
  onPressGameDetail,
}: {
  matchup: Matchup;
  onPressGameDetail?: () => void;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const userTz = useUserTimeZone();

  const spread = matchup.spreadCurrent;
  const spreadOpen = matchup.spreadOpen ?? null;
  const ou = matchup.overUnderCurrent;
  const ouOpen = matchup.overUnderOpen ?? null;
  const hasSpread = spread != null;
  const hasOu = ou != null && ou !== 0;

  const sArrow = spreadArrow(spread, spreadOpen);
  const oArrow = ouArrow(ou, ouOpen);

  const cityState = [matchup.venueCity, matchup.venueState]
    .filter(Boolean)
    .join(', ');

  return (
    <View style={styles.compactMeta}>
      {(hasSpread || hasOu) && (
        <View style={styles.compactOddsStack}>
          {hasSpread && (
            <View style={styles.compactOddsLine}>
              <Text style={[styles.oddsLabel, { color: theme.textMuted }]}>SPREAD </Text>
              {sArrow && (
                <Text style={[styles.oddsArrow, { color: sArrow.color }]}>{sArrow.symbol}</Text>
              )}
              <Text style={[styles.oddsValue, { color: theme.tint }]}>
                {spreadLabel(spread)}
              </Text>
            </View>
          )}
          {hasOu && (
            <View style={styles.compactOddsLine}>
              <Text style={[styles.oddsLabel, { color: theme.textMuted }]}>O/U </Text>
              {oArrow && (
                <Text style={[styles.oddsArrow, { color: oArrow.color }]}>{oArrow.symbol}</Text>
              )}
              <Text style={[styles.oddsValue, { color: theme.tint }]}>{ou}</Text>
            </View>
          )}
        </View>
      )}

      <Text style={[styles.compactTime, { color: theme.text }]}>
        {formatToUserTime(matchup.startDateUtc, userTz)}
      </Text>

      {matchup.broadcasts ? (
        <Text style={[styles.compactMetaText, { color: theme.textMuted }]} numberOfLines={3}>
          {matchup.broadcasts}
        </Text>
      ) : null}

      {matchup.venue ? (
        <Text style={[styles.compactMetaText, { color: theme.textMuted }]} numberOfLines={1}>
          {matchup.venue}
        </Text>
      ) : null}

      {cityState ? (
        <Text style={[styles.compactMetaText, { color: theme.textMuted }]} numberOfLines={1}>
          {cityState}
        </Text>
      ) : null}

      <OverviewLink
        label="Game Preview"
        onPress={onPressGameDetail}
        theme={theme}
        align="flex-start"
      />
    </View>
  );
}

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

  if (isSelected && pickResult === 'correct') {
    borderColor = '#16A34A'; bgColor = '#F0FDF4'; teamColor = '#16A34A';
  } else if (isSelected && pickResult === 'incorrect') {
    borderColor = '#DC2626'; bgColor = '#FEF2F2'; teamColor = '#DC2626';
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
      {/* ✓ when selected and not incorrect */}
      {isSelected && pickResult !== 'incorrect' && (
        <Text style={[styles.pickIcon, { color: pickResult === 'correct' ? '#16A34A' : Colors.brand.navy }]}>✓</Text>
      )}
      {/* ✗ when selected + incorrect */}
      {isSelected && pickResult === 'incorrect' && (
        <Text style={[styles.pickIcon, { color: '#DC2626' }]}>✗</Text>
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
}

export function MatchupCard({ matchup, pick, onPress, onPressTeam, onPick, seasonYear, leagueSport }: MatchupCardProps) {
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

  const status = enrichedMatchup.status.toLowerCase();
  const isFinal = status === 'final' || status === 'completed';
  // Compact 2-column layout only applies pre-game — once the game starts,
  // the score column reactivates and the single-column flow makes more sense.
  const isScheduled = status === 'scheduled';

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

  const year = seasonYear ?? new Date(matchup.startDateUtc).getFullYear();

  const handleOpenStats = async () => {
    setShowStats(true);
    if (statsData) return; // already loaded
    setStatsLoading(true);
    try {
      const [awayStats, homeStats, awayMetrics, homeMetrics] = await Promise.all([
        teamCardApi.getStatistics(matchup.awaySlug, year, matchup.awayFranchiseSeasonId),
        teamCardApi.getStatistics(matchup.homeSlug, year, matchup.homeFranchiseSeasonId),
        teamCardApi.getMetrics(matchup.awaySlug, year, matchup.awayFranchiseSeasonId),
        teamCardApi.getMetrics(matchup.homeSlug, year, matchup.homeFranchiseSeasonId),
      ]);
      setStatsData({
        teamA: { name: matchup.away, logoUri: matchup.awayLogoUri, stats: awayStats, metrics: awayMetrics },
        teamB: { name: matchup.home, logoUri: matchup.homeLogoUri, stats: homeStats, metrics: homeMetrics },
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

  const homeIsWinning =
    enrichedMatchup.homeScore != null &&
    enrichedMatchup.awayScore != null &&
    enrichedMatchup.homeScore >= enrichedMatchup.awayScore;

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

  // Card border color based on pick result (matches web)
  let cardBorderColor = theme.border;
  if (isFinal && hasPick) {
    if (isPickCorrect === true) cardBorderColor = '#16A34A';
    else if (isPickCorrect === false) cardBorderColor = '#DC2626';
  } else if (isFinal && !hasPick) {
    cardBorderColor = '#DC2626'; // missed pick
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
        { backgroundColor: theme.card, borderColor: cardBorderColor },
        isFinal && hasPick && isPickCorrect === true && styles.cardCorrect,
        isFinal && (isPickCorrect === false || !hasPick) && styles.cardIncorrect,
      ]}
    >
      {/* Headline banner */}
      {matchup.headLine != null && matchup.headLine !== '' && (
        <View style={styles.headline}>
          <Text style={styles.headlineText} numberOfLines={1}>{matchup.headLine}</Text>
        </View>
      )}

      {isScheduled ? (
        // ── Compact 2-column layout: team rows on the left, meta on the
        // right. Active only pre-game; once the game starts, the score
        // column reactivates and the standard single-column flow returns.
        <View style={styles.compactRow}>
          <View style={styles.compactLeft}>
            <TouchableOpacity
              onPress={onPressTeam ? () => onPressTeam('away') : undefined}
              activeOpacity={onPressTeam ? 0.6 : 1}
              disabled={!onPressTeam}
            >
              <TeamRow
                matchup={enrichedMatchup}
                side="away"
                isWinning={!homeIsWinning}
                isPicked={pickedAway}
                isPickCorrect={isPickCorrect}
                isFinal={isFinal}
              />
            </TouchableOpacity>
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
              />
            </TouchableOpacity>
          </View>
          <View style={styles.compactRight}>
            <ScheduledMeta matchup={enrichedMatchup} onPressGameDetail={onPress} />
          </View>
        </View>
      ) : (
        <>
          {/* Away team — taps go to the team page, not the contest overview. */}
          <TouchableOpacity
            onPress={onPressTeam ? () => onPressTeam('away') : undefined}
            activeOpacity={onPressTeam ? 0.6 : 1}
            disabled={!onPressTeam}
          >
            <TeamRow
              matchup={enrichedMatchup}
              side="away"
              isWinning={!homeIsWinning}
              isPicked={pickedAway}
              isPickCorrect={isPickCorrect}
              isFinal={isFinal}
            />
          </TouchableOpacity>

          {/* Home team */}
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
            />
          </TouchableOpacity>

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
          />
        </>
      )}

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

  // Compact 2-column scheduled layout
  compactRow: {
    flexDirection: 'row',
    alignItems: 'stretch',
  },
  compactLeft: {
    flex: 65,
  },
  compactRight: {
    flex: 35,
    paddingHorizontal: 10,
    paddingVertical: 10,
    justifyContent: 'flex-start',
  },
  compactMeta: {
    gap: 4,
  },
  compactOddsStack: {
    gap: 2,
    marginBottom: 4,
  },
  compactOddsLine: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
  },
  compactTime: {
    fontSize: 12,
    fontWeight: '600',
  },
  compactMetaText: {
    fontSize: 11,
    textAlign: 'center',
  },

  // Headline
  headline: {
    backgroundColor: Colors.brand.navy,
    paddingVertical: 6,
    paddingHorizontal: 14,
  },
  headlineText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    textAlign: 'center',
  },

  // Team row
  teamRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 14,
    paddingVertical: 10,
    gap: 10,
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
  recordText: {
    fontSize: 12,
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
    fontSize: 11,
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
