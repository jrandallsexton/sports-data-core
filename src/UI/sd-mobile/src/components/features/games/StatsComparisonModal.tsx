import React, { useEffect, useMemo, useState } from 'react';
import {
  Modal,
  View,
  ScrollView,
  TouchableOpacity,
  Image,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Colors, getTheme } from '@/constants/Colors';
import type {
  ContestAtsBucketFact,
  ContestHistoryGame,
  ContestMarginFact,
  ContestPriorSeasonSummary,
  ContestSpreadContext,
  Matchup,
  TeamComparisonData,
  TeamStatEntry,
} from '@/src/types/models';
import { usePageSheetTopInset } from '@/src/hooks/usePageSheetTopInset';
import { useSectionCollapse } from '@/src/hooks/useSectionCollapse';
import { Wordmark } from '@/src/components/brand/Wordmark';
import { Ionicons } from '@expo/vector-icons';
import { contrastTextOn, resolveTeamColors } from '@/src/utils/teamColor';

// ─── Props ────────────────────────────────────────────────────────────────────

interface StatsComparisonModalProps {
  visible: boolean;
  onClose: () => void;
  matchup: Matchup;
  comparison: TeamComparisonData | null;
  isLoading: boolean;
  /** Gambling-content gate (spread / ATS / O-U lines in history rows). */
  showGambling: boolean;
}

// ─── Team header ──────────────────────────────────────────────────────────────

function TeamHeader({
  name,
  logoUri,
  color,
  align,
}: {
  name: string;
  logoUri?: string | null;
  color?: string | null;
  align: 'left' | 'right';
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const isRight = align === 'right';
  return (
    <View style={[styles.teamHeader, isRight && styles.teamHeaderRight]}>
      {!isRight && (
        logoUri ? (
          <Image source={{ uri: logoUri }} style={styles.teamLogo} />
        ) : (
          <View style={[styles.teamLogoPlaceholder, { backgroundColor: color ?? Colors.brand.navy }]}>
            <Text style={styles.teamLogoInitial}>{name?.[0] ?? '?'}</Text>
          </View>
        )
      )}
      <Text
        numberOfLines={1}
        style={[styles.teamHeaderName, { color: theme.text }, isRight && { textAlign: 'right' }]}
      >
        {name}
      </Text>
      {isRight && (
        logoUri ? (
          <Image source={{ uri: logoUri }} style={styles.teamLogo} />
        ) : (
          <View style={[styles.teamLogoPlaceholder, { backgroundColor: color ?? Colors.brand.navy }]}>
            <Text style={styles.teamLogoInitial}>{name?.[0] ?? '?'}</Text>
          </View>
        )
      )}
    </View>
  );
}

// ─── Category tab ─────────────────────────────────────────────────────────────

// ─── Metrics comparison spec — WEB PARITY ─────────────────────────────────────
// Mirrors sd-ui TeamComparison's metricsData exactly (labels, formats,
// higher-is-better polarity, and the category grouping). netPunt /
// penaltyYardsPerPlay intentionally absent (metrics formula audit M4/H3).
export type MetricSpec = {
  label: string;
  key: string;
  format: (val: number | null | undefined) => string;
  higherIsBetter: boolean;
};

// Null/absent metric values render '-' (parseNumeric maps it to null:
// no bar, no favored tint) — a fabricated '0.0%' is indistinguishable
// from a real 0%. RzTdRate/RzScoreRate/OppRzTdRate/FgPctShrunk are
// nullable through the whole chain (no red-zone possessions, no
// qualifying FG attempts).
const pct = (val: number | null | undefined) => (val != null ? (val * 100).toFixed(1) + '%' : '-');
const dec2 = (val: number | null | undefined) => (val != null ? val.toFixed(2) : '-');

export const METRICS_SPEC: { category: string; metrics: MetricSpec[] }[] = [
  {
    category: 'Offensive Efficiency',
    metrics: [
      { label: 'Yards Per Play', key: 'ypp', format: dec2, higherIsBetter: true },
      { label: 'Success Rate', key: 'successRate', format: pct, higherIsBetter: true },
      { label: 'Explosive Play Rate', key: 'explosiveRate', format: pct, higherIsBetter: true },
      { label: 'Points Per Drive', key: 'pointsPerDrive', format: dec2, higherIsBetter: true },
      { label: '3rd/4th Down Rate', key: 'thirdFourthRate', format: pct, higherIsBetter: true },
    ],
  },
  {
    category: 'Red Zone Efficiency',
    metrics: [
      { label: 'Red Zone TD Rate', key: 'rzTdRate', format: pct, higherIsBetter: true },
      { label: 'Red Zone Score Rate', key: 'rzScoreRate', format: pct, higherIsBetter: true },
    ],
  },
  {
    category: 'Defensive Metrics',
    metrics: [
      { label: 'Opp Yards Per Play', key: 'oppYpp', format: dec2, higherIsBetter: false },
      { label: 'Opp Success Rate', key: 'oppSuccessRate', format: pct, higherIsBetter: false },
      { label: 'Opp Explosive Rate', key: 'oppExplosiveRate', format: pct, higherIsBetter: false },
      { label: 'Opp Points Per Drive', key: 'oppPointsPerDrive', format: dec2, higherIsBetter: false },
      { label: 'Opp 3rd/4th Down Rate', key: 'oppThirdFourthRate', format: pct, higherIsBetter: false },
      { label: 'Opp Red Zone TD Rate', key: 'oppRzTdRate', format: pct, higherIsBetter: false },
    ],
  },
  {
    category: 'Game Control',
    metrics: [
      { label: 'Time Possession Ratio', key: 'timePossRatio', format: pct, higherIsBetter: true },
      { label: 'Field Position Differential', key: 'fieldPosDiff', format: dec2, higherIsBetter: true },
      {
        label: 'Turnover Margin Per Drive',
        key: 'turnoverMarginPerDrive',
        format: (val) => (val != null ? val.toFixed(3) : '-'),
        higherIsBetter: true,
      },
    ],
  },
  {
    category: 'Special Teams',
    metrics: [
      {
        label: 'Field Goal %',
        key: 'fgPctShrunk',
        format: (val) => (val != null ? (val * 100).toFixed(1) + '%' : '-'),
        higherIsBetter: true,
      },
    ],
  },
];

/** Which side a stat row favors — web parity: numeric compare of the
    display values, inverted for lower-is-better stats. */
export function statFavored(away?: TeamStatEntry, home?: TeamStatEntry): 'away' | 'home' | null {
  if (!away || !home) return null;
  const a = parseFloat(away.displayValue ?? '');
  const b = parseFloat(home.displayValue ?? '');
  if (Number.isNaN(a) || Number.isNaN(b)) return null;
  const isNegative = away.isNegativeAttribute ?? home.isNegativeAttribute ?? false;
  if (isNegative) return a < b ? 'away' : b < a ? 'home' : null;
  return a > b ? 'away' : b > a ? 'home' : null;
}

/**
 * Each side's share of one full-width bar: away/(away+home) of the row's
 * two values, so 89.1% vs 61.3% completion reads 59/41 and 10 vs 7 avg
 * gain reads 59/41 - the SIZE of the edge, which the favored chip cannot
 * show. Lower-is-better stats take the opponent's value instead (INT 1 vs
 * 0 -> 0/100 to the team with 0), so the bar never contradicts the chip.
 * Null - no bar - on a tie, a non-number, both zero, or a negative value
 * (a share of a signed quantity like turnover differential means nothing;
 * the chip still marks the better side).
 */
export function statShare(away?: TeamStatEntry, home?: TeamStatEntry): { away: number; home: number } | null {
  if (!away || !home) return null;
  const a = parseFloat(away.displayValue ?? '');
  const b = parseFloat(home.displayValue ?? '');
  if (Number.isNaN(a) || Number.isNaN(b) || a < 0 || b < 0 || a === b) return null;
  const total = a + b;
  if (total <= 0) return null;
  const isNegative = away.isNegativeAttribute ?? home.isNegativeAttribute ?? false;
  const awayShare = (isNegative ? b : a) / total;
  return { away: awayShare, home: 1 - awayShare };
}

/**
 * A row that says nothing about either team only pads the category: both
 * sides zero (2-Pt Pass Att 0 vs 0), both the API's "—" placeholder, or
 * one of each. A row is kept as soon as either side has a real non-zero
 * number.
 */
export function isEmptyStatRow(away?: TeamStatEntry, home?: TeamStatEntry): boolean {
  const blank = (e?: TeamStatEntry) => {
    const n = parseFloat(e?.displayValue ?? '');
    return Number.isNaN(n) || n === 0;
  };
  return blank(away) && blank(home);
}

export function metricFavored(spec: MetricSpec, a?: number | null, b?: number | null): 'away' | 'home' | null {
  if (a == null || b == null) return null;
  if (spec.higherIsBetter) return a > b ? 'away' : b > a ? 'home' : null;
  return a < b ? 'away' : b < a ? 'home' : null;
}

type FavoredTally = { away: number; home: number };

/**
 * The web's "gradient bar": one thin track split in proportion to how many
 * rows each side leads, painted in the teams' colors. Reads as "who owns
 * this category" before the numbers do - the whole point of the team-color
 * scheme. Hidden when neither side leads anything.
 */
function FavoredSplitBar({
  tally,
  awayColor,
  homeColor,
}: {
  tally: FavoredTally;
  awayColor: string;
  homeColor: string;
}) {
  const total = tally.away + tally.home;
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  if (total === 0) return null;
  return (
    <View style={[styles.splitBarTrack, splitBarTrackTheme(theme)]}>
      <View style={{ flex: tally.away, backgroundColor: awayColor }} />
      <View style={{ flex: tally.home, backgroundColor: homeColor }} />
    </View>
  );
}

/**
 * A faint fill and a hairline outline under every split bar. Without them
 * a team whose color sits near the page background (Wake Forest black on
 * the dark theme) has an invisible segment and every bar reads as "the
 * other team, then nothing".
 */
function splitBarTrackTheme(theme: { border: string; textMuted: string }) {
  return { backgroundColor: theme.border, borderColor: theme.textMuted, borderWidth: StyleSheet.hairlineWidth };
}

function CategoryTab({
  label,
  active,
  onPress,
  tally,
  awayColor,
  homeColor,
}: {
  label: string;
  active: boolean;
  onPress: () => void;
  tally?: FavoredTally;
  awayColor?: string;
  homeColor?: string;
}) {
  return (
    <TouchableOpacity
      onPress={onPress}
      style={[styles.tab, active && { backgroundColor: Colors.brand.navy, borderColor: Colors.brand.navy }]}
    >
      <Text style={[styles.tabText, active && { color: '#fff' }]}>{label}</Text>
      {tally && awayColor && homeColor && (
        <FavoredSplitBar tally={tally} awayColor={awayColor} homeColor={homeColor} />
      )}
    </TouchableOpacity>
  );
}

// ─── One stat comparison row ──────────────────────────────────────────────────

function StatRow({
  label,
  awayEntry,
  homeEntry,
  favored = null,
  share = null,
  awayColor = Colors.brand.navy,
  homeColor = Colors.brand.navy,
}: {
  label: string;
  awayEntry: TeamStatEntry;
  homeEntry: TeamStatEntry;
  /** Normalized "#rrggbb" team colors; the favored value is painted on its team's color. */
  awayColor?: string;
  homeColor?: string;
  /** Which side leads this row; null = tie/incomparable. Web parity:
      the leading value is highlighted, and lower-is-better stats invert. */
  favored?: 'away' | 'home' | null;
  /** Each side's share of the row's single bar (statShare); null = no bar. */
  share?: { away: number; home: number } | null;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  return (
    <View style={[styles.statRowBlock, { borderBottomColor: theme.border }]}>
    <View style={styles.statRow}>
      {/* Away value */}
      <View style={[styles.statValue, styles.statValueLeft]}>
        <View style={styles.statValueLine}>
          <Text
            style={[
              styles.statValueText,
              { color: theme.text },
              favored === 'away' && [styles.favoredChip, { backgroundColor: awayColor, color: contrastTextOn(awayColor) }],
            ]}
          >
            {awayEntry.displayValue}
          </Text>
          {awayEntry.rank != null && awayEntry.rank > 1 && (
            <Text style={[styles.statRank, { color: theme.textMuted }]}> (#{awayEntry.rank})</Text>
          )}
        </View>
      </View>

      {/* Label */}
      <View style={styles.statLabelBox}>
        <Text numberOfLines={2} style={[styles.statLabel, { color: theme.textMuted }]}>{label}</Text>
      </View>

      {/* Home value */}
      <View style={[styles.statValue, styles.statValueRight]}>
        <View style={[styles.statValueLine, styles.statValueLineRight]}>
          {homeEntry.rank != null && homeEntry.rank > 1 && (
            <Text style={[styles.statRank, { color: theme.textMuted }]}>(#{homeEntry.rank}) </Text>
          )}
          <Text
            style={[
              styles.statValueText,
              styles.statValueTextRight,
              { color: theme.text },
              favored === 'home' && [styles.favoredChip, { backgroundColor: homeColor, color: contrastTextOn(homeColor) }],
            ]}
          >
            {homeEntry.displayValue}
          </Text>
        </View>
      </View>
    </View>

    {/* One bar for the row, split at each side's share (statShare): the
        size of the edge, in team colors, the same way the tab and category
        headers show the tally. The chip marks who is better; this shows
        by how much. */}
    {share && (
      <View style={[styles.splitBarTrack, splitBarTrackTheme(theme)]} testID="stat-share-bar">
        <View testID="stat-share-away" style={{ flex: share.away, backgroundColor: awayColor }} />
        <View testID="stat-share-home" style={{ flex: share.home, backgroundColor: homeColor }} />
        {/* 50% mark, as on the DeetsMeter: without it a 55/45 split and a
            70/30 split read the same at a glance. */}
        <View style={styles.shareMidline} pointerEvents="none" />
      </View>
    )}
    </View>
  );
}

// ─── History pieces ───────────────────────────────────────────────────────────

/**
 * Section title that doubles as a collapse toggle. Sections stay EXPANDED by
 * default — the card exists to surface context without being asked — so this
 * is an escape valve for readers who find a section noisy, not a gate in
 * front of the content.
 */
function CollapsibleSectionHeader({
  title,
  collapsed,
  onToggle,
  color,
  mutedColor,
  titleIndent = 0,
  tally,
  awayColor,
  homeColor,
}: {
  title: string;
  collapsed: boolean;
  onToggle: () => void;
  color: string;
  mutedColor: string;
  /** Left padding on the title, for headers nested under a tab rather than flush with it. */
  titleIndent?: number;
  /** When given with both colors, a team-colored split bar renders under the title. */
  tally?: FavoredTally;
  awayColor?: string;
  homeColor?: string;
}) {
  return (
    <TouchableOpacity
      onPress={onToggle}
      style={styles.collapsibleHeader}
      hitSlop={{ top: 6, bottom: 6, left: 6, right: 6 }}
      accessibilityRole="button"
      accessibilityState={{ expanded: !collapsed }}
      accessibilityLabel={`${title}, ${collapsed ? 'collapsed' : 'expanded'}`}
    >
      <View style={[styles.collapsibleTitleBox, { paddingLeft: titleIndent }]}>
        <Text style={[styles.historySectionTitle, { color }]}>{title}</Text>
        {tally && awayColor && homeColor && (
          <FavoredSplitBar tally={tally} awayColor={awayColor} homeColor={homeColor} />
        )}
      </View>
      <Ionicons
        name={collapsed ? 'chevron-down' : 'chevron-up'}
        size={18}
        color={mutedColor}
      />
    </TouchableOpacity>
  );
}

function formatGameDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

/**
 * The short name when the payload carries a usable one, else the full name.
 * Empty counts as absent: the column is required but not non-empty, and a
 * blank team name next to a score is worse than a long one.
 */
function shortOr(short: string | null | undefined, full: string): string {
  return short && short.trim().length > 0 ? short : full;
}

/**
 * Display name for a head-to-head participant. Identity fields (winner,
 * spreadWinner) carry the full Franchise.DisplayName; render the short name
 * for that side when the payload has it, else fall back to the full name.
 */
function h2hShortName(g: ContestHistoryGame, fullName: string | null | undefined): string | null | undefined {
  if (fullName == null) return fullName;
  if (fullName === g.homeTeam) return shortOr(g.homeTeamShort, fullName);
  if (fullName === g.awayTeam) return shortOr(g.awayTeamShort, fullName);
  return fullName;
}

/**
 * One game of a team's prior-season tail, from that team's perspective.
 * Historical team names come from Franchise.DisplayName — the same source as
 * Matchup.away/home — so exact string matching identifies "our" side.
 */
function PriorSeasonGameRow({ game, teamName }: { game: ContestHistoryGame; teamName: string }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const isHome = game.homeTeam === teamName;
  const isAway = game.awayTeam === teamName;
  if (!isHome && !isAway) {
    // Defensive: name matched neither side — render the neutral line.
    return (
      <View style={[styles.historyGameRow, { borderBottomColor: theme.border }]}>
        <Text style={[styles.historyGameDetail, { color: theme.text }]} numberOfLines={1}>
          {game.awayTeam} {game.awayScore ?? '—'} @ {game.homeTeam} {game.homeScore ?? '—'}
        </Text>
        <Text style={[styles.historyGameDate, { color: theme.textMuted }]}>
          {formatGameDate(game.gameDate)}
        </Text>
      </View>
    );
  }

  const ourScore = isHome ? game.homeScore : game.awayScore;
  const theirScore = isHome ? game.awayScore : game.homeScore;
  const outcome = game.winner == null ? 'T' : game.winner === teamName ? 'W' : 'L';
  const badgeColors =
    outcome === 'W'
      ? { color: theme.successText, backgroundColor: theme.successBg }
      : outcome === 'L'
        ? { color: theme.errorText, backgroundColor: theme.errorBg }
        : { color: theme.textMuted, backgroundColor: 'transparent' };

  return (
    <View style={[styles.historyGameRow, { borderBottomColor: theme.border }]}>
      <Text style={[styles.historyResultBadge, badgeColors]}>{outcome}</Text>
      <Text style={[styles.historyGameScore, { color: theme.text }]}>
        {ourScore ?? '—'}-{theirScore ?? '—'}
      </Text>
      <Text style={[styles.historyGameDetail, { color: theme.text }]} numberOfLines={1}>
        {isHome ? 'vs' : '@'} {isHome ? shortOr(game.awayTeamShort, game.awayTeam) : shortOr(game.homeTeamShort, game.homeTeam)}
      </Text>
      <Text style={[styles.historyGameDate, { color: theme.textMuted }]}>
        {formatGameDate(game.gameDate)}
      </Text>
    </View>
  );
}

function priorSeasonRecordLabel(summary: ContestPriorSeasonSummary | null | undefined): string | null {
  if (!summary) return null;
  const conf =
    summary.conferenceWins != null && summary.conferenceLosses != null
      ? ` (${summary.conferenceWins}-${summary.conferenceLosses} conf)`
      : '';
  return `${summary.seasonYear}: ${summary.wins}-${summary.losses}${conf}`;
}

// ─── "The Line" — spread-context fact sentences ───────────────────────────────
// Deterministic sentences composed from server-computed facts — every number
// comes from a query, never from prose. Mirrors the web dialog's wording.

type LineFact = { head: string; detail: string; windowGames?: string[] };

function marginFactSentence(
  teamName: string,
  displayName: string,
  fact: ContestMarginFact | null | undefined,
  magnitude: number,
  won: boolean,
): LineFact | null {
  if (!fact) return null;
  if (!fact.lastGame) {
    return {
      head: `${displayName} ${won ? 'has never won' : 'has never lost'} a game by ${magnitude}+`,
      detail: `in our records (back to ${fact.searchFloorSeason}).`,
    };
  }
  const g = fact.lastGame;
  const isHome = g.homeTeam === teamName;
  const ourScore = isHome ? g.homeScore : g.awayScore;
  const theirScore = isHome ? g.awayScore : g.homeScore;
  const opponent = isHome ? shortOr(g.awayTeamShort, g.awayTeam) : shortOr(g.homeTeamShort, g.homeTeam);
  const when = formatGameDate(g.gameDate);
  const quality =
    fact.opponentSeasonRecord || fact.opponentPriorSeasonRecord
      ? ` (they went ${fact.opponentSeasonRecord ?? '?'}${
          fact.opponentPriorSeasonRecord ? `; ${fact.opponentPriorSeasonRecord} the season before` : ''
        })`
      : '';
  const times = fact.countLastFiveSeasons;
  // The games behind the count, newest first — "8 such wins" invites exactly
  // one question ("against whom?") and these lines answer it. Web twin:
  // TeamComparison.jsx marginFactSentence.
  const windowGames = (fact.windowGames ?? []).map((gm) => {
    const yr = `'${String(gm.seasonYear).slice(-2)}`;
    const rec = gm.opponentSeasonRecord ? ` (${gm.opponentSeasonRecord})` : '';
    return `${yr} ${shortOr(gm.opponentShort, gm.opponent)} ${gm.teamScore}-${gm.opponentScore}${rec}`;
  });
  return {
    head: `Last time ${displayName} ${won ? 'won' : 'lost'} by ${magnitude}+:`,
    detail: `${when} — ${won ? 'beat' : 'lost to'} ${opponent} ${ourScore ?? '—'}-${theirScore ?? '—'}${quality}. ${times} such ${won ? 'win' : 'loss'}${times === 1 ? '' : won ? 's' : 'es'} in the last 5 seasons.`,
    windowGames,
  };
}

function atsFactSentence(
  displayName: string,
  fact: ContestAtsBucketFact | null | undefined,
  asFavorite: boolean,
): LineFact | null {
  if (!fact) return null;
  // Band, not open-ended: a -12.5 line renders "as a 10–14 point favorite"
  // — the cohort the line actually sits in. thresholdUpper is null only
  // above the top rung, where "49+" is honestly unbounded. Web twin:
  // TeamComparison.jsx atsFactSentence.
  const role =
    fact.thresholdUpper != null
      ? `${fact.threshold}–${fact.thresholdUpper} point ${asFavorite ? 'favorite' : 'underdog'}`
      : `${fact.threshold}+ ${asFavorite ? 'favorite' : 'underdog'}`;
  if (fact.games === 0) {
    return {
      head: `${displayName} as a ${role}:`,
      detail: `no games with a line ${fact.thresholdUpper != null ? 'in that range' : 'that large'} since ${fact.dataFloorSeason}.`,
    };
  }
  // The games behind the count, newest first — "covered 16 of 28" invites
  // exactly one question ("against whom?") and these lines answer it. Web
  // twin: TeamComparison.jsx atsFactSentence. Server caps at 10.
  const windowGames = (fact.windowGames ?? []).map((gm) => {
    const yr = `'${String(gm.seasonYear).slice(-2)}`;
    const rec = gm.opponentSeasonRecord ? ` (${gm.opponentSeasonRecord})` : '';
    const line = gm.teamSpread > 0 ? `+${gm.teamSpread}` : String(gm.teamSpread);
    return `${yr} ${shortOr(gm.opponentShort, gm.opponent)} ${gm.teamScore}-${gm.opponentScore}${rec} · ${line} ${gm.covered ? '✓' : '✗'}`;
  });
  return {
    head: `${displayName} as a ${role}:`,
    detail: `covered ${fact.covers} of ${fact.games} (since ${fact.dataFloorSeason}).`,
    windowGames,
  };
}

/**
 * Sentence team names are display-only; identity inside the facts still
 * matches on the full name (favoriteTeam/underdogTeam are Franchise.DisplayName,
 * the same source as Matchup.home/away). shortFor maps a full name to the
 * matchup's short name for the sentence heads.
 */
function spreadContextFacts(ctx: ContestSpreadContext, shortFor: (fullName: string) => string): LineFact[] {
  return [
    marginFactSentence(ctx.favoriteTeam, shortFor(ctx.favoriteTeam), ctx.favoriteWonByMargin, ctx.magnitude, true),
    marginFactSentence(ctx.underdogTeam, shortFor(ctx.underdogTeam), ctx.underdogLostByMargin, ctx.magnitude, false),
    atsFactSentence(shortFor(ctx.favoriteTeam), ctx.favoriteAtsAsBigFavorite, true),
    atsFactSentence(shortFor(ctx.underdogTeam), ctx.underdogAtsAsBigUnderdog, false),
  ].filter((f): f is LineFact => f != null);
}

/**
 * One category's stat rows, away vs home, aligned by index (the payload lists
 * both sides in the same order). The payload's human name is statisticValue;
 * label/name are legacy fallbacks and the index-based one is last resort.
 */
function StatCategoryRows({
  category,
  awayRows,
  homeRows,
  mutedColor,
  awayColor,
  homeColor,
}: {
  category: string;
  awayRows: TeamStatEntry[];
  homeRows: TeamStatEntry[];
  mutedColor: string;
  awayColor: string;
  homeColor: string;
}) {
  const rowCount = Math.max(awayRows.length, homeRows.length);
  const visibleCount = Array.from({ length: rowCount }, (_, i) => i)
    .filter((i) => (awayRows[i] || homeRows[i]) && !isEmptyStatRow(awayRows[i], homeRows[i])).length;
  if (rowCount === 0 || visibleCount === 0) {
    return (
      <Text style={[styles.emptyText, { color: mutedColor, padding: 24 }]}>
        No {category} stats available.
      </Text>
    );
  }
  return (
    <>
      {Array.from({ length: rowCount }, (_, i) => {
        const away = awayRows[i];
        const home = homeRows[i];
        if (!away && !home) return null;
        if (isEmptyStatRow(away, home)) return null;
        const label =
          away?.statisticValue ??
          home?.statisticValue ??
          (away as any)?.label ??
          (away as any)?.name ??
          (home as any)?.label ??
          (home as any)?.name ??
          `Stat ${i + 1}`;
        return (
          <StatRow
            key={away?.statisticKey ?? home?.statisticKey ?? i}
            label={label}
            awayEntry={away ?? { displayValue: '—' }}
            homeEntry={home ?? { displayValue: '—' }}
            favored={statFavored(away, home)}
            share={statShare(away, home)}
            awayColor={awayColor}
            homeColor={homeColor}
          />
        );
      })}
    </>
  );
}

/**
 * A stats category as a collapsible section. Its own component so the
 * per-category collapse hook has a stable key ("stats.<category>") - the
 * same persisted, per-device collapse the History sections use.
 */
function StatCategorySection({
  category,
  title,
  tally,
  awayRows,
  homeRows,
  headerColor,
  mutedColor,
  awayColor,
  homeColor,
}: {
  category: string;
  title: string;
  tally?: FavoredTally;
  awayRows: TeamStatEntry[];
  homeRows: TeamStatEntry[];
  /** Title + chevron color. Muted, like the Metrics group headers - the team-colored split bar carries the emphasis. */
  headerColor: string;
  mutedColor: string;
  awayColor: string;
  homeColor: string;
}) {
  return (
    <CollapsibleGroup
      storageKey={`stats.${category}`}
      title={title}
      tally={tally}
      headerColor={headerColor}
      awayColor={awayColor}
      homeColor={homeColor}
    >
      <StatCategoryRows
        category={category}
        awayRows={awayRows}
        homeRows={homeRows}
        mutedColor={mutedColor}
        awayColor={awayColor}
        homeColor={homeColor}
      />
    </CollapsibleGroup>
  );
}

/**
 * A group of rows behind a collapsible header, COLLAPSED by default and
 * persisted per device under storageKey. Stats categories and Metrics groups
 * share this so the two tabs read the same way: muted title, team-colored
 * split bar, chevron.
 */
function CollapsibleGroup({
  storageKey,
  title,
  tally,
  headerColor,
  awayColor,
  homeColor,
  children,
}: {
  storageKey: string;
  title: string;
  tally?: FavoredTally;
  headerColor: string;
  awayColor: string;
  homeColor: string;
  children: React.ReactNode;
}) {
  const { collapsed, toggle } = useSectionCollapse(storageKey, true);
  return (
    <View>
      <CollapsibleSectionHeader
        title={title}
        collapsed={collapsed}
        onToggle={toggle}
        color={headerColor}
        mutedColor={headerColor}
        titleIndent={12}
        tally={tally}
        awayColor={awayColor}
        homeColor={homeColor}
      />
      {!collapsed && children}
    </View>
  );
}

// ─── StatsComparisonModal ─────────────────────────────────────────────────────

export function StatsComparisonModal({
  visible,
  onClose,
  matchup,
  comparison,
  isLoading,
  showGambling,
}: StatsComparisonModalProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const topInset = usePageSheetTopInset();

  // Collapse state is per device and survives reopening the card — a toggle
  // that reset every time would have to be redone on every matchup.
  const { collapsed: h2hCollapsed, toggle: toggleH2h } = useSectionCollapse('history.headToHead');
  const { collapsed: lastSeasonCollapsed, toggle: toggleLastSeason } =
    useSectionCollapse('history.lastSeason');

  // The Line is the exception: collapsible, but NOT persisted — it
  // re-expands on every dialog launch (owner call 2026-09-12). It is the
  // headline context yet the tallest block on mobile; readers collapse it
  // to reach the sections below, and expect it back for the next game.
  const [lineCollapsed, setLineCollapsed] = useState(false);
  useEffect(() => {
    // Reset on CLOSE, not open: the effect runs post-commit, so resetting
    // on open would paint one collapsed frame before expanding.
    if (!visible) setLineCollapsed(false);
  }, [visible]);

  // Collect all category names from teamA stats
  const awayStats = comparison?.teamA?.stats?.data?.statistics ?? {};
  const homeStats = comparison?.teamB?.stats?.data?.statistics ?? {};
  const categories = Object.keys(awayStats).length > 0
    ? Object.keys(awayStats)
    : Object.keys(homeStats);

  // Metrics ride the same comparison payload (fetched by MatchupCard);
  // the tab renders only when both sides have them — the same
  // both-or-nothing stance the rest of the platform takes.
  const awayMetrics = (comparison?.teamA?.metrics?.data ?? null) as Record<string, number | null> | null;
  const homeMetrics = (comparison?.teamB?.metrics?.data ?? null) as Record<string, number | null> | null;
  // Non-null is NOT enough: the API deliberately returns HTTP 200 with an
  // EMPTY zeroed DTO when metrics haven't been generated (or the backend
  // call soft-failed) — gating on null alone would render "Metrics (0:0)"
  // full of fake 0.00s. gamesPlayed is the honest emptiness signal: a
  // metric row only exists once games have been played.
  const hasMetrics =
    ((awayMetrics?.gamesPlayed as number | undefined) ?? 0) > 0 &&
    ((homeMetrics?.gamesPlayed as number | undefined) ?? 0) > 0;

  // Favored tallies — web parity: tab labels read "Stats (95:60)" /
  // "Metrics (4:6)", category chips carry their own counts.
  const favoredByCategory = useMemo(() => {
    const perCategory: Record<string, { away: number; home: number }> = {};
    let away = 0;
    let home = 0;
    // ESPN files the same stat under several categories (Net Total Yds,
    // Points, Offensive Plays sit in passing, rushing AND receiving). Each
    // category counts its own rows, but the tab-level total counts a FACT
    // once - otherwise one edge is worth three. A fact is the key plus both
    // values plus polarity: ESPN also reuses a key for different stats
    // ("touchbacks" is kickoffs in kicking and punts in punting, with
    // different numbers and opposite polarity), and those must each count.
    const counted = new Set<string>();
    for (const cat of categories) {
      const a = awayStats[cat] ?? [];
      const h = homeStats[cat] ?? [];
      const tally = { away: 0, home: 0 };
      for (let i = 0; i < Math.max(a.length, h.length); i++) {
        const f = statFavored(a[i], h[i]);
        if (f === null) continue;
        if (f === 'away') tally.away++;
        if (f === 'home') tally.home++;
        const key = [
          a[i]?.statisticKey ?? h[i]?.statisticKey ?? `${cat}:${i}`,
          a[i]?.displayValue ?? '',
          h[i]?.displayValue ?? '',
          a[i]?.isNegativeAttribute ?? h[i]?.isNegativeAttribute ?? false,
        ].join('|');
        if (counted.has(key)) continue;
        counted.add(key);
        if (f === 'away') away++;
        else home++;
      }
      perCategory[cat] = tally;
    }
    return { perCategory, away, home };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [comparison]);

  const metricsFavored = useMemo(() => {
    const tally = { away: 0, home: 0 };
    if (!hasMetrics) return tally;
    for (const group of METRICS_SPEC) {
      for (const m of group.metrics) {
        const f = metricFavored(m, awayMetrics?.[m.key], homeMetrics?.[m.key]);
        if (f === 'away') tally.away++;
        if (f === 'home') tally.home++;
      }
    }
    return tally;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [comparison, hasMetrics]);

  // Historical blocks (head-to-head + prior-season form) — present whenever
  // the franchises have played before, including week 1 when stats are empty.
  const history = comparison?.history ?? null;
  // Full name -> the matchup's short name ("Miami", not the "MIA" abbreviation),
  // for narrow-surface display only.
  const shortNameFor = (fullName: string): string =>
    fullName === matchup.away ? matchup.awayShortName || fullName
    : fullName === matchup.home ? matchup.homeShortName || fullName
    : fullName;
  // Team colors drive the favored chips and split bars (web parity). A team
  // without a color falls back to brand navy; if that leaves both sides the
  // same color, home takes the theme's neutral so the bars still split.
  const { away: awayColor, home: homeColor } = resolveTeamColors(
    matchup.awayColor,
    matchup.homeColor,
    Colors.brand.navy,
    theme.textMuted,
  );
  const headToHead = history?.headToHead ?? [];
  // Rolling "Last N Games" (current + prior season), added 2026-09-09;
  // fall back to the prior-season lists against an older API payload.
  const awayPriorGames = history?.awayRecentGames?.length
    ? history.awayRecentGames
    : history?.awayPriorSeasonGames ?? [];
  const homePriorGames = history?.homeRecentGames?.length
    ? history.homeRecentGames
    : history?.homePriorSeasonGames ?? [];
  const hasHistory =
    headToHead.length > 0 ||
    awayPriorGames.length > 0 ||
    homePriorGames.length > 0 ||
    history?.awayPriorSeason != null ||
    history?.homePriorSeason != null ||
    // Spread context only counts when it can actually render — it is
    // gambling-gated, and a hidden-only history would open an empty tab.
    (showGambling && history?.spreadContext != null);

  // Head-to-head wins among the displayed meetings (ties count for neither).
  const h2hWinsAway = headToHead.filter((g) => g.winner === matchup.away).length;
  const h2hWinsHome = headToHead.filter((g) => g.winner === matchup.home).length;

  // History is the overview and leads (matches the web dialog); Stats is the
  // detail tab. Null until the user picks, so the default can settle after
  // the data arrives.
  const [mainTabChoice, setMainTabChoice] = useState<'history' | 'stats' | 'metrics' | null>(null);
  // Default tab: history when it exists, else stats, else metrics — the
  // last case keeps the Metrics tab reachable when both statistics slots
  // came back empty (a week-1 first meeting with a stats soft-failure).
  const mainTab =
    mainTabChoice ?? (hasHistory ? 'history' : categories.length > 0 ? 'stats' : hasMetrics ? 'metrics' : 'stats');

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      // Android: force the modal window edge-to-edge on EVERY API level so
      // the usePageSheetTopInset padding is always the right amount — without
      // this, hosts that place the modal below the status bar would get the
      // status-bar offset AND the padding (double spacing). iOS ignores it.
      statusBarTranslucent
      onRequestClose={onClose}
    >
      <View style={[styles.container, { backgroundColor: theme.background, paddingTop: topInset }]}>
        {/* Header */}
        <View style={[styles.header, { borderBottomColor: theme.border }]}>
          <View style={styles.headerLeft} />
          {/* Visual identity is the brand; SEMANTIC identity stays the
              sheet name - without the label both modals announce an
              identical "sportDeets" to VoiceOver/TalkBack and cannot be
              told apart (Vortex, PR #746). */}
          <View accessible accessibilityRole="header" accessibilityLabel="Team Comparison">
            <Wordmark size={17} />
          </View>
          <TouchableOpacity onPress={onClose} style={styles.closeBtn} hitSlop={12}>
            <Text style={[styles.closeText, { color: theme.textMuted }]}>✕</Text>
          </TouchableOpacity>
        </View>

        {isLoading ? (
          <View style={styles.loadingContainer}>
            <ActivityIndicator size="large" color={Colors.brand.navy} />
            <Text style={[styles.loadingText, { color: theme.textMuted }]}>
              Loading stats…
            </Text>
          </View>
        ) : comparison == null || (categories.length === 0 && !hasHistory && !hasMetrics) ? (
          <View style={styles.loadingContainer}>
            <Text style={[styles.emptyText, { color: theme.textMuted }]}>
              Stats not available.
            </Text>
          </View>
        ) : (
          <View style={styles.body}>
            {/* Team headers */}
            <View style={[styles.teamsRow, { borderBottomColor: theme.border }]}>
              {/* Short names ("Miami", not "MIA"): two full names do not fit a phone header. */}
              <TeamHeader
                name={matchup.awayShortName || comparison.teamA.name}
                logoUri={comparison.teamA.logoUri}
                color={matchup.awayColor}
                align="left"
              />
              <TeamHeader
                name={matchup.homeShortName || comparison.teamB.name}
                logoUri={comparison.teamB.logoUri}
                color={matchup.homeColor}
                align="right"
              />
            </View>

            {/* Main tabs — History is the overview and leads (when it
                exists); Stats carries the category detail; Metrics mirrors
                the web's Metrics tab. Counts are favored-stat tallies,
                same as the web's "Statistics (95:60)" / "Metrics (4:6)". */}
            {/* Horizontal ScrollView, not a plain row: with counts on
                every label, three chips (~370dp intrinsic) overflow a
                360dp Android viewport and the rightmost chip — Metrics —
                is the one pushed off-screen (Vortex, PR #751). Same
                escape hatch as the category-chip row below. */}
            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              style={[styles.mainTabsScroll, { borderBottomColor: theme.border }]}
              contentContainerStyle={styles.mainTabsRow}
            >
              {hasHistory && (
                <CategoryTab
                  label={`History (${h2hWinsAway}:${h2hWinsHome})`}
                  active={mainTab === 'history'}
                  onPress={() => setMainTabChoice('history')}
                  tally={{ away: h2hWinsAway, home: h2hWinsHome }}
                  awayColor={awayColor}
                  homeColor={homeColor}
                />
              )}
              <CategoryTab
                label={`Stats (${favoredByCategory.away}:${favoredByCategory.home})`}
                active={mainTab === 'stats'}
                onPress={() => setMainTabChoice('stats')}
                tally={favoredByCategory}
                awayColor={awayColor}
                homeColor={homeColor}
              />
              {hasMetrics && (
                <CategoryTab
                  label={`Metrics (${metricsFavored.away}:${metricsFavored.home})`}
                  active={mainTab === 'metrics'}
                  onPress={() => setMainTabChoice('metrics')}
                  tally={metricsFavored}
                  awayColor={awayColor}
                  homeColor={homeColor}
                />
              )}
            </ScrollView>

            {mainTab === 'history' && hasHistory ? (
              <ScrollView
                showsVerticalScrollIndicator={false}
                contentContainerStyle={styles.historyContent}
              >
                {showGambling && history?.spreadContext && (
                  <>
                    <CollapsibleSectionHeader
                      title={`The Line${history.spreadContext.spreadDetails ? `: ${history.spreadContext.spreadDetails}` : ''}`}
                      collapsed={lineCollapsed}
                      onToggle={() => setLineCollapsed((c) => !c)}
                      color={theme.textMuted}
                      mutedColor={theme.textMuted}
                    />
                    {!lineCollapsed && spreadContextFacts(history.spreadContext, shortNameFor).map((f, i) => (
                      <View key={i} style={[styles.lineFact, { borderBottomColor: theme.border }]}>
                        <Text style={[styles.lineFactText, { color: theme.text }]}>
                          <Text style={styles.lineFactHead}>{f.head}</Text> {f.detail}
                        </Text>
                        {(f.windowGames?.length ?? 0) > 0 && (
                          <View style={styles.lineFactGames}>
                            {f.windowGames!.map((g, j) => (
                              <Text
                                key={j}
                                style={[styles.lineFactGame, { color: theme.textMuted }]}
                              >
                                {g}
                              </Text>
                            ))}
                          </View>
                        )}
                      </View>
                    ))}
                  </>
                )}
                {headToHead.length > 0 && (
                  <>
                    <CollapsibleSectionHeader
                      title={`Head-to-Head: Last ${headToHead.length} Meeting${headToHead.length === 1 ? '' : 's'}`}
                      collapsed={h2hCollapsed}
                      onToggle={toggleH2h}
                      color={theme.textMuted}
                      mutedColor={theme.textMuted}
                    />
                    {!h2hCollapsed && headToHead.map((g, i) => (
                      <View key={i} style={[styles.h2hRow, { borderBottomColor: theme.border }]}>
                        <View style={styles.h2hMeta}>
                          <Text style={[styles.historyGameDate, { color: theme.textMuted }]}>
                            {formatGameDate(g.gameDate)}
                          </Text>
                          {g.phase && g.phase !== 'Regular Season' && (
                            <Text style={[styles.h2hPhase, { color: theme.textMuted }]}>{g.phase}</Text>
                          )}
                          {!!g.note && (
                            <Text style={[styles.h2hPhase, { color: theme.textMuted }]} numberOfLines={1}>
                              {g.note}
                            </Text>
                          )}
                        </View>
                        {/* Winner is marked by weight alone — team-color text
                            is illegible when a team's color sits near the
                            background (matches the web dialog). */}
                        <View style={styles.h2hLine}>
                          <Text
                            style={[
                              styles.h2hTeam,
                              { color: theme.text },
                              g.winner === g.awayTeam && styles.h2hWinner,
                            ]}
                            numberOfLines={1}
                          >
                            {shortOr(g.awayTeamShort, g.awayTeam)} {g.awayScore ?? '—'}
                          </Text>
                          <Text style={[styles.h2hAt, { color: theme.textMuted }]}>@</Text>
                          <Text
                            style={[
                              styles.h2hTeam,
                              { color: theme.text },
                              g.winner === g.homeTeam && styles.h2hWinner,
                            ]}
                            numberOfLines={1}
                          >
                            {shortOr(g.homeTeamShort, g.homeTeam)} {g.homeScore ?? '—'}
                          </Text>
                        </View>
                        {showGambling && (g.spread || g.spreadWinner || g.overUnderResult) && (
                          <View style={styles.h2hMeta}>
                            {!!g.spread && (
                              <Text style={[styles.h2hMarket, { color: theme.textMuted }]}>{g.spread}</Text>
                            )}
                            {!!g.spreadWinner && (
                              <Text style={[styles.h2hMarket, { color: theme.textMuted }]}>
                                ATS: {h2hShortName(g, g.spreadWinner)}
                              </Text>
                            )}
                            {!!g.overUnderResult && (
                              <Text style={[styles.h2hMarket, { color: theme.textMuted }]}>
                                {g.overUnderResult}
                                {g.overUnder != null ? ` ${g.overUnder}` : ''}
                              </Text>
                            )}
                          </View>
                        )}
                      </View>
                    ))}
                  </>
                )}

                <CollapsibleSectionHeader
                  title={`Last ${Math.max(awayPriorGames.length, homePriorGames.length)} Games`}
                  collapsed={lastSeasonCollapsed}
                  onToggle={toggleLastSeason}
                  color={theme.textMuted}
                  mutedColor={theme.textMuted}
                />

                {!lastSeasonCollapsed && (
                <>
                <View style={styles.historyTeamHeader}>
                  <Text style={[styles.historyTeamName, { color: theme.text }]}>{matchup.away}</Text>
                  {priorSeasonRecordLabel(history?.awayPriorSeason) && (
                    <Text style={[styles.historySeasonRecord, { color: theme.textMuted }]}>
                      {priorSeasonRecordLabel(history?.awayPriorSeason)}
                    </Text>
                  )}
                </View>
                {awayPriorGames.length === 0 ? (
                  <Text style={[styles.historyEmptyNote, { color: theme.textMuted }]}>
                    No prior-season games on record.
                  </Text>
                ) : (
                  awayPriorGames.map((g, i) => (
                    <PriorSeasonGameRow key={i} game={g} teamName={matchup.away} />
                  ))
                )}

                <View style={styles.historyTeamHeader}>
                  <Text style={[styles.historyTeamName, { color: theme.text }]}>{matchup.home}</Text>
                  {priorSeasonRecordLabel(history?.homePriorSeason) && (
                    <Text style={[styles.historySeasonRecord, { color: theme.textMuted }]}>
                      {priorSeasonRecordLabel(history?.homePriorSeason)}
                    </Text>
                  )}
                </View>
                {homePriorGames.length === 0 ? (
                  <Text style={[styles.historyEmptyNote, { color: theme.textMuted }]}>
                    No prior-season games on record.
                  </Text>
                ) : (
                  homePriorGames.map((g, i) => (
                    <PriorSeasonGameRow key={i} game={g} teamName={matchup.home} />
                  ))
                )}
                </>
                )}
              </ScrollView>
            ) : mainTab === 'metrics' && hasMetrics ? (
              /* Metrics tab — web parity: the same grouped metric list,
                 formats, and polarity as sd-ui's TeamComparison. Rows reuse
                 StatRow; favored is computed on the RAW values (formatted
                 percents would mislead parseFloat for lower-is-better). */
              <ScrollView showsVerticalScrollIndicator={false}>
                {METRICS_SPEC.map((group) => {
                  const groupTally: FavoredTally = { away: 0, home: 0 };
                  for (const m of group.metrics) {
                    const f = metricFavored(m, awayMetrics?.[m.key], homeMetrics?.[m.key]);
                    if (f === 'away') groupTally.away++;
                    if (f === 'home') groupTally.home++;
                  }
                  return (
                    <CollapsibleGroup
                      key={group.category}
                      storageKey={`metrics.${group.category}`}
                      title={`${group.category} (${groupTally.away}:${groupTally.home})`}
                      tally={groupTally}
                      headerColor={theme.textMuted}
                      awayColor={awayColor}
                      homeColor={homeColor}
                    >
                      {group.metrics.map((m) => (
                        <StatRow
                          key={m.key}
                          label={m.label}
                          awayEntry={{ displayValue: m.format(awayMetrics?.[m.key]) }}
                          homeEntry={{ displayValue: m.format(homeMetrics?.[m.key]) }}
                          favored={metricFavored(m, awayMetrics?.[m.key], homeMetrics?.[m.key])}
                          awayColor={awayColor}
                          homeColor={homeColor}
                        />
                      ))}
                    </CollapsibleGroup>
                  );
                })}
              </ScrollView>
            ) : categories.length === 0 ? (
              <View style={styles.loadingContainer}>
                <Text style={[styles.emptyText, { color: theme.textMuted }]}>
                  Stats not available.
                </Text>
              </View>
            ) : (
              /* Every category stacked in one vertical scroll - no swiping
                 between categories. Each is a collapsible section like the
                 History ones (persisted per device), because the full list
                 is far too many rows to read top to bottom. */
              <ScrollView showsVerticalScrollIndicator={false}>
                {categories.map((cat) => {
                  const tally = favoredByCategory.perCategory[cat];
                  // The slug is the key (and the collapse key); the label is the
                  // category's ShortDisplayName, carried on every entry.
                  const catLabel =
                    awayStats[cat]?.[0]?.categoryDisplayName ??
                    homeStats[cat]?.[0]?.categoryDisplayName ??
                    cat;
                  return (
                    <StatCategorySection
                      key={cat}
                      category={cat}
                      title={tally ? `${catLabel} (${tally.away}:${tally.home})` : catLabel}
                      tally={tally}
                      awayRows={awayStats[cat] ?? []}
                      homeRows={homeStats[cat] ?? []}
                      headerColor={theme.textMuted}
                      mutedColor={theme.textMuted}
                      awayColor={awayColor}
                      homeColor={homeColor}
                    />
                  );
                })}
              </ScrollView>
            )}
          </View>
        )}
      </View>
    </Modal>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  headerLeft: {
    width: 32,
  },
  closeBtn: {
    width: 32,
    alignItems: 'flex-end',
  },
  closeText: {
    fontSize: 17,
  },
  loadingContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 12,
  },
  loadingText: {
    fontSize: 15,
  },
  emptyText: {
    fontSize: 15,
  },
  body: {
    flex: 1,
  },

  // Teams row
  teamsRow: {
    flexDirection: 'row',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: StyleSheet.hairlineWidth,
    gap: 8,
  },
  teamHeader: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  teamHeaderRight: {
    justifyContent: 'flex-end',
  },
  teamLogo: {
    width: 32,
    height: 32,
    resizeMode: 'contain',
  },
  teamLogoPlaceholder: {
    width: 32,
    height: 32,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
  },
  teamLogoInitial: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '700',
  },
  teamHeaderName: {
    flex: 1,
    fontSize: 13,
    fontWeight: '700',
  },

  // Category tabs
  tabScroll: {
    borderBottomWidth: StyleSheet.hairlineWidth,
    flexGrow: 0,
    // flexShrink 0 is load-bearing: without it the stat-rows ScrollView
    // below compresses this row and the chips render vertically clipped
    // (visible once chip labels grew with the (a:b) counts).
    flexShrink: 0,
  },
  tabScrollContent: {
    paddingHorizontal: 12,
    paddingVertical: 8,
    gap: 8,
  },
  tab: {
    paddingHorizontal: 14,
    paddingVertical: 6,
    borderRadius: 20,
    borderWidth: 1.5,
    borderColor: '#CBD5E1',
  },
  tabText: {
    fontSize: 13,
    fontWeight: '600',
    color: '#64748B',
  },
  // Team-colored split bar (web's "category-gradient-bar"): thin, full
  // width of whatever it sits under, segments in proportion to the tally.
  splitBarTrack: {
    flexDirection: 'row',
    height: 5,
    borderRadius: 3,
    overflow: 'hidden',
    marginTop: 4,
    alignSelf: 'stretch',
  },
  shareMidline: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    left: '50%',
    width: 2,
    marginLeft: -1,
    backgroundColor: 'rgba(255, 255, 255, 0.3)',
  },
  // A favored value painted on its team's color; text flips to whichever
  // of light/dark reads against it (contrastTextOn).
  favoredChip: {
    paddingHorizontal: 6,
    paddingVertical: 1,
    borderRadius: 4,
    overflow: 'hidden',
  },
  collapsibleTitleBox: {
    flex: 1,
  },

  // Stat rows
  statRowBlock: {
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderBottomWidth: StyleSheet.hairlineWidth,
    gap: 6,
  },
  statRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: 8,
  },
  statValue: {
    flex: 1,
    gap: 4,
  },
  statValueLeft: {
    alignItems: 'flex-start',
  },
  statValueRight: {
    alignItems: 'flex-end',
  },
  statValueText: {
    fontSize: 14,
    fontWeight: '700',
  },
  statValueTextRight: {
    textAlign: 'right',
  },
  statValueLine: {
    flexDirection: 'row',
    alignItems: 'baseline',
  },
  statValueLineRight: {
    justifyContent: 'flex-end',
  },
  statRank: {
    fontSize: 10,
  },
  statLabelBox: {
    width: 90,
    alignItems: 'center',
  },
  statLabel: {
    fontSize: 11,
    fontWeight: '500',
    textAlign: 'center',
    lineHeight: 14,
  },
  // Main tabs (History | Stats)
  mainTabsScroll: {
    borderBottomWidth: StyleSheet.hairlineWidth,
    flexGrow: 0,
    flexShrink: 0,
  },
  mainTabsRow: {
    flexDirection: 'row',
    gap: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },

  // History tab
  historyContent: {
    paddingHorizontal: 16,
    paddingTop: 12,
    paddingBottom: 24,
    gap: 4,
  },
  historySectionTitle: {
    fontSize: 15,
    fontWeight: '700',
    marginTop: 12,
    marginBottom: 6,
  },
  collapsibleHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 8,
  },
  h2hRow: {
    paddingVertical: 8,
    borderBottomWidth: StyleSheet.hairlineWidth,
    gap: 2,
  },
  h2hMeta: {
    flexDirection: 'row',
    gap: 10,
    alignItems: 'baseline',
  },
  h2hPhase: {
    fontSize: 11,
    fontStyle: 'italic',
  },
  h2hLine: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: 6,
  },
  h2hTeam: {
    fontSize: 14,
    flexShrink: 1,
  },
  h2hWinner: {
    fontWeight: '700',
  },
  h2hAt: {
    fontSize: 12,
  },
  h2hMarket: {
    fontSize: 11,
  },
  historyTeamHeader: {
    marginTop: 10,
    marginBottom: 4,
    gap: 1,
  },
  historyTeamName: {
    fontSize: 13,
    fontWeight: '700',
  },
  historySeasonRecord: {
    fontSize: 12,
  },
  historyGameRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: 8,
    paddingVertical: 6,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  historyResultBadge: {
    width: 20,
    textAlign: 'center',
    fontSize: 12,
    fontWeight: '700',
    borderRadius: 4,
    overflow: 'hidden',
  },
  historyGameScore: {
    fontSize: 13,
    fontWeight: '600',
    fontVariant: ['tabular-nums'],
  },
  historyGameDetail: {
    flex: 1,
    fontSize: 13,
  },
  historyGameDate: {
    fontSize: 11,
  },
  historyEmptyNote: {
    fontSize: 12,
    fontStyle: 'italic',
    paddingVertical: 4,
  },
  lineFact: {
    paddingVertical: 6,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  lineFactText: {
    fontSize: 13,
    lineHeight: 19,
  },
  lineFactHead: {
    fontWeight: '700',
  },
  // Evidence list under a margin fact — muted, tabular, one game per line.
  lineFactGames: {
    marginTop: 4,
    gap: 1,
  },
  lineFactGame: {
    fontSize: 12,
    fontVariant: ['tabular-nums'],
  },

});
