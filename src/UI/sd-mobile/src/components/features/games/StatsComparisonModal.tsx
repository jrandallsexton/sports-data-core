import React, { useMemo, useState } from 'react';
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

const pct = (val: number | null | undefined) => (val ? (val * 100).toFixed(1) + '%' : '0.0%');
const dec2 = (val: number | null | undefined) => val?.toFixed(2) ?? '0.00';

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
        format: (val) => val?.toFixed(3) ?? '0.000',
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

export function metricFavored(spec: MetricSpec, a?: number | null, b?: number | null): 'away' | 'home' | null {
  if (a == null || b == null) return null;
  if (spec.higherIsBetter) return a > b ? 'away' : b > a ? 'home' : null;
  return a < b ? 'away' : b < a ? 'home' : null;
}

function CategoryTab({
  label,
  active,
  onPress,
}: {
  label: string;
  active: boolean;
  onPress: () => void;
}) {
  return (
    <TouchableOpacity
      onPress={onPress}
      style={[styles.tab, active && { backgroundColor: Colors.brand.navy, borderColor: Colors.brand.navy }]}
    >
      <Text style={[styles.tabText, active && { color: '#fff' }]}>{label}</Text>
    </TouchableOpacity>
  );
}

// ─── Parse a displayValue string into a number for bar sizing ─────────────────

function parseNumeric(displayValue: string): number | null {
  const match = displayValue.match(/[-\d.]+/);
  if (!match) return null;
  const n = parseFloat(match[0]);
  return isNaN(n) ? null : n;
}

// ─── One stat comparison row ──────────────────────────────────────────────────

function StatRow({
  label,
  awayEntry,
  homeEntry,
  favored = null,
}: {
  label: string;
  awayEntry: TeamStatEntry;
  homeEntry: TeamStatEntry;
  /** Which side leads this row; null = tie/incomparable. Web parity:
      the leading value is highlighted, and lower-is-better stats invert. */
  favored?: 'away' | 'home' | null;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const awayNum = parseNumeric(awayEntry.displayValue ?? '');
  const homeNum = parseNumeric(homeEntry.displayValue ?? '');
  const max = awayNum != null && homeNum != null ? Math.max(Math.abs(awayNum), Math.abs(homeNum)) : null;

  const awayPct = max && max > 0 ? Math.abs(awayNum!) / max : 0;
  const homePct = max && max > 0 ? Math.abs(homeNum!) / max : 0;

  return (
    <View style={[styles.statRow, { borderBottomColor: theme.border }]}>
      {/* Away value */}
      <View style={[styles.statValue, styles.statValueLeft]}>
        <View style={styles.statValueLine}>
          <Text style={[styles.statValueText, { color: favored === 'away' ? theme.tint : theme.text }]}>
            {awayEntry.displayValue}
          </Text>
          {awayEntry.rank != null && awayEntry.rank > 1 && (
            <Text style={[styles.statRank, { color: theme.textMuted }]}> (#{awayEntry.rank})</Text>
          )}
        </View>
        {max != null && (
          <View style={styles.barTrack}>
            <View
              style={[
                styles.bar,
                styles.barRight,
                { width: `${awayPct * 100}%`, backgroundColor: Colors.brand.navy },
              ]}
            />
          </View>
        )}
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
          <Text style={[styles.statValueText, styles.statValueTextRight, { color: favored === 'home' ? theme.tint : theme.text }]}>
            {homeEntry.displayValue}
          </Text>
        </View>
        {max != null && (
          <View style={styles.barTrack}>
            <View
              style={[
                styles.bar,
                styles.barLeft,
                { width: `${homePct * 100}%`, backgroundColor: Colors.brand.navy },
              ]}
            />
          </View>
        )}
      </View>
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
}: {
  title: string;
  collapsed: boolean;
  onToggle: () => void;
  color: string;
  mutedColor: string;
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
      <Text style={[styles.historySectionTitle, { color }]}>{title}</Text>
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
        {isHome ? 'vs' : '@'} {isHome ? game.awayTeam : game.homeTeam}
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
  fact: ContestMarginFact | null | undefined,
  magnitude: number,
  won: boolean,
): LineFact | null {
  if (!fact) return null;
  if (!fact.lastGame) {
    return {
      head: `${teamName} ${won ? 'has never won' : 'has never lost'} a game by ${magnitude}+`,
      detail: `in our records (back to ${fact.searchFloorSeason}).`,
    };
  }
  const g = fact.lastGame;
  const isHome = g.homeTeam === teamName;
  const ourScore = isHome ? g.homeScore : g.awayScore;
  const theirScore = isHome ? g.awayScore : g.homeScore;
  const opponent = isHome ? g.awayTeam : g.homeTeam;
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
    return `${yr} ${gm.opponent} ${gm.teamScore}-${gm.opponentScore}${rec}`;
  });
  return {
    head: `Last time ${teamName} ${won ? 'won' : 'lost'} by ${magnitude}+:`,
    detail: `${when} — ${won ? 'beat' : 'lost to'} ${opponent} ${ourScore ?? '—'}-${theirScore ?? '—'}${quality}. ${times} such ${won ? 'win' : 'loss'}${times === 1 ? '' : won ? 's' : 'es'} in the last 5 seasons.`,
    windowGames,
  };
}

function atsFactSentence(
  teamName: string,
  fact: ContestAtsBucketFact | null | undefined,
  asFavorite: boolean,
): LineFact | null {
  if (!fact) return null;
  const role = `${fact.threshold}+ ${asFavorite ? 'favorite' : 'underdog'}`;
  if (fact.games === 0) {
    return {
      head: `${teamName} as a ${role}:`,
      detail: `no games with a line that large since ${fact.dataFloorSeason}.`,
    };
  }
  return {
    head: `${teamName} as a ${role}:`,
    detail: `covered ${fact.covers} of ${fact.games} (since ${fact.dataFloorSeason}).`,
  };
}

function spreadContextFacts(ctx: ContestSpreadContext): LineFact[] {
  return [
    marginFactSentence(ctx.favoriteTeam, ctx.favoriteWonByMargin, ctx.magnitude, true),
    marginFactSentence(ctx.underdogTeam, ctx.underdogLostByMargin, ctx.magnitude, false),
    atsFactSentence(ctx.favoriteTeam, ctx.favoriteAtsAsBigFavorite, true),
    atsFactSentence(ctx.underdogTeam, ctx.underdogAtsAsBigUnderdog, false),
  ].filter((f): f is LineFact => f != null);
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

  const [activeCategory, setActiveCategory] = useState<string | null>(null);

  // Collapse state is per device and survives reopening the card — a toggle
  // that reset every time would have to be redone on every matchup.
  const { collapsed: h2hCollapsed, toggle: toggleH2h } = useSectionCollapse('history.headToHead');
  const { collapsed: lastSeasonCollapsed, toggle: toggleLastSeason } =
    useSectionCollapse('history.lastSeason');

  // Collect all category names from teamA stats
  const awayStats = comparison?.teamA?.stats?.data?.statistics ?? {};
  const homeStats = comparison?.teamB?.stats?.data?.statistics ?? {};
  const categories = Object.keys(awayStats).length > 0
    ? Object.keys(awayStats)
    : Object.keys(homeStats);

  const currentCategory = activeCategory ?? categories[0] ?? null;

  const awayRows: TeamStatEntry[] = currentCategory ? (awayStats[currentCategory] ?? []) : [];
  const homeRows: TeamStatEntry[] = currentCategory ? (homeStats[currentCategory] ?? []) : [];
  const rowCount = Math.max(awayRows.length, homeRows.length);

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
    for (const cat of categories) {
      const a = awayStats[cat] ?? [];
      const h = homeStats[cat] ?? [];
      const tally = { away: 0, home: 0 };
      for (let i = 0; i < Math.max(a.length, h.length); i++) {
        const f = statFavored(a[i], h[i]);
        if (f === 'away') tally.away++;
        if (f === 'home') tally.home++;
      }
      perCategory[cat] = tally;
      away += tally.away;
      home += tally.home;
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
              <TeamHeader
                name={comparison.teamA.name}
                logoUri={comparison.teamA.logoUri}
                color={matchup.awayColor}
                align="left"
              />
              <TeamHeader
                name={comparison.teamB.name}
                logoUri={comparison.teamB.logoUri}
                color={matchup.homeColor}
                align="right"
              />
            </View>

            {/* Main tabs — History is the overview and leads (when it
                exists); Stats carries the category detail; Metrics mirrors
                the web's Metrics tab. Counts are favored-stat tallies,
                same as the web's "Statistics (95:60)" / "Metrics (4:6)". */}
            <View style={[styles.mainTabsRow, { borderBottomColor: theme.border }]}>
              {hasHistory && (
                <CategoryTab
                  label={`History (${h2hWinsAway}:${h2hWinsHome})`}
                  active={mainTab === 'history'}
                  onPress={() => setMainTabChoice('history')}
                />
              )}
              <CategoryTab
                label={`Stats (${favoredByCategory.away}:${favoredByCategory.home})`}
                active={mainTab === 'stats'}
                onPress={() => setMainTabChoice('stats')}
              />
              {hasMetrics && (
                <CategoryTab
                  label={`Metrics (${metricsFavored.away}:${metricsFavored.home})`}
                  active={mainTab === 'metrics'}
                  onPress={() => setMainTabChoice('metrics')}
                />
              )}
            </View>

            {mainTab === 'history' && hasHistory ? (
              <ScrollView
                showsVerticalScrollIndicator={false}
                contentContainerStyle={styles.historyContent}
              >
                {showGambling && history?.spreadContext && (
                  <>
                    <Text style={[styles.historySectionTitle, { color: theme.tint }]}>
                      The Line
                      {history.spreadContext.spreadDetails ? ` — ${history.spreadContext.spreadDetails}` : ''}
                    </Text>
                    {spreadContextFacts(history.spreadContext).map((f, i) => (
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
                      title={`Head-to-Head — Last ${headToHead.length} Meeting${headToHead.length === 1 ? '' : 's'}`}
                      collapsed={h2hCollapsed}
                      onToggle={toggleH2h}
                      color={theme.tint}
                      mutedColor={theme.tint}
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
                            {g.awayTeam} {g.awayScore ?? '—'}
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
                            {g.homeTeam} {g.homeScore ?? '—'}
                          </Text>
                        </View>
                        {showGambling && (g.spread || g.spreadWinner || g.overUnderResult) && (
                          <View style={styles.h2hMeta}>
                            {!!g.spread && (
                              <Text style={[styles.h2hMarket, { color: theme.textMuted }]}>{g.spread}</Text>
                            )}
                            {!!g.spreadWinner && (
                              <Text style={[styles.h2hMarket, { color: theme.textMuted }]}>
                                ATS: {g.spreadWinner}
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
                  color={theme.tint}
                  mutedColor={theme.tint}
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
                {METRICS_SPEC.map((group) => (
                  <View key={group.category}>
                    <Text style={[styles.metricsGroupHeader, { color: theme.textMuted, borderBottomColor: theme.border }]}>
                      {group.category}
                    </Text>
                    {group.metrics.map((m) => (
                      <StatRow
                        key={m.key}
                        label={m.label}
                        awayEntry={{ displayValue: m.format(awayMetrics?.[m.key]) }}
                        homeEntry={{ displayValue: m.format(homeMetrics?.[m.key]) }}
                        favored={metricFavored(m, awayMetrics?.[m.key], homeMetrics?.[m.key])}
                      />
                    ))}
                  </View>
                ))}
              </ScrollView>
            ) : categories.length === 0 ? (
              <View style={styles.loadingContainer}>
                <Text style={[styles.emptyText, { color: theme.textMuted }]}>
                  Stats not available.
                </Text>
              </View>
            ) : (
              <>
                {/* Category tabs */}
                <ScrollView
                  horizontal
                  showsHorizontalScrollIndicator={false}
                  style={[styles.tabScroll, { borderBottomColor: theme.border }]}
                  contentContainerStyle={styles.tabScrollContent}
                >
                  {categories.map((cat) => {
                    const tally = favoredByCategory.perCategory[cat];
                    const chipLabel = tally ? `${cat} (${tally.away}:${tally.home})` : cat;
                    return (
                      <CategoryTab
                        key={cat}
                        label={chipLabel}
                        active={currentCategory === cat}
                        onPress={() => setActiveCategory(cat)}
                      />
                    );
                  })}
                </ScrollView>

                {/* Stat rows */}
                <ScrollView showsVerticalScrollIndicator={false}>
                  {rowCount === 0 ? (
                    <Text style={[styles.emptyText, { color: theme.textMuted, padding: 24 }]}>
                      No {currentCategory} stats available.
                    </Text>
                  ) : (
                    Array.from({ length: rowCount }, (_, i) => {
                      const away = awayRows[i];
                      const home = homeRows[i];
                      // The payload's human name is statisticValue (what the
                      // web renders); label/name are legacy fallbacks. The
                      // index-based fallback is last resort only.
                      const label =
                        away?.statisticValue ??
                        home?.statisticValue ??
                        (away as any)?.label ??
                        (away as any)?.name ??
                        (home as any)?.label ??
                        (home as any)?.name ??
                        `Stat ${i + 1}`;

                      if (!away && !home) return null;

                      return (
                        <StatRow
                          key={away?.statisticKey ?? home?.statisticKey ?? i}
                          label={label}
                          awayEntry={away ?? { displayValue: '—' }}
                          homeEntry={home ?? { displayValue: '—' }}
                          favored={statFavored(away, home)}
                        />
                      );
                    })
                  )}
                </ScrollView>
              </>
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

  // Stat rows
  statRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderBottomWidth: StyleSheet.hairlineWidth,
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
  metricsGroupHeader: {
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    paddingHorizontal: 16,
    paddingTop: 14,
    paddingBottom: 6,
    borderBottomWidth: StyleSheet.hairlineWidth,
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
  mainTabsRow: {
    flexDirection: 'row',
    gap: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderBottomWidth: StyleSheet.hairlineWidth,
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

  barTrack: {
    width: '100%',
    height: 4,
    backgroundColor: '#E2E8F0',
    borderRadius: 2,
    overflow: 'hidden',
  },
  bar: {
    height: 4,
    borderRadius: 2,
    maxWidth: '100%',
  },
  barRight: {
    alignSelf: 'flex-start',
  },
  barLeft: {
    alignSelf: 'flex-end',
  },
});
