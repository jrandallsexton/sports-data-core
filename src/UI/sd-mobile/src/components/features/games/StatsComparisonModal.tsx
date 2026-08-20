import React, { useState } from 'react';
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
}: {
  label: string;
  awayEntry: TeamStatEntry;
  homeEntry: TeamStatEntry;
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
        <Text style={[styles.statValueText, { color: theme.text }]}>{awayEntry.displayValue}</Text>
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
        <Text style={[styles.statValueText, styles.statValueTextRight, { color: theme.text }]}>
          {homeEntry.displayValue}
        </Text>
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

type LineFact = { head: string; detail: string };

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
  return {
    head: `Last time ${teamName} ${won ? 'won' : 'lost'} by ${magnitude}+:`,
    detail: `${when} — ${won ? 'beat' : 'lost to'} ${opponent} ${ourScore ?? '—'}-${theirScore ?? '—'}${quality}. ${times} such ${won ? 'win' : 'loss'}${times === 1 ? '' : won ? 's' : 'es'} in the last 5 seasons.`,
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

  // Historical blocks (head-to-head + prior-season form) — present whenever
  // the franchises have played before, including week 1 when stats are empty.
  const history = comparison?.history ?? null;
  const headToHead = history?.headToHead ?? [];
  const awayPriorGames = history?.awayPriorSeasonGames ?? [];
  const homePriorGames = history?.homePriorSeasonGames ?? [];
  const hasHistory =
    headToHead.length > 0 ||
    awayPriorGames.length > 0 ||
    homePriorGames.length > 0 ||
    history?.awayPriorSeason != null ||
    history?.homePriorSeason != null ||
    history?.spreadContext != null;

  // Head-to-head wins among the displayed meetings (ties count for neither).
  const h2hWinsAway = headToHead.filter((g) => g.winner === matchup.away).length;
  const h2hWinsHome = headToHead.filter((g) => g.winner === matchup.home).length;

  // History is the overview and leads (matches the web dialog); Stats is the
  // detail tab. Null until the user picks, so the default can settle after
  // the data arrives.
  const [mainTabChoice, setMainTabChoice] = useState<'history' | 'stats' | null>(null);
  const mainTab = mainTabChoice ?? (hasHistory ? 'history' : 'stats');

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
          <Text style={[styles.headerTitle, { color: theme.text }]}>Team Comparison</Text>
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
        ) : comparison == null || (categories.length === 0 && !hasHistory) ? (
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

            {/* Main tabs — History is the overview and leads; Stats carries
                the category detail. Only shown when history exists. */}
            {hasHistory && (
              <View style={[styles.mainTabsRow, { borderBottomColor: theme.border }]}>
                <CategoryTab
                  label={`History (${h2hWinsAway}:${h2hWinsHome})`}
                  active={mainTab === 'history'}
                  onPress={() => setMainTabChoice('history')}
                />
                <CategoryTab
                  label="Stats"
                  active={mainTab === 'stats'}
                  onPress={() => setMainTabChoice('stats')}
                />
              </View>
            )}

            {mainTab === 'history' && hasHistory ? (
              <ScrollView
                showsVerticalScrollIndicator={false}
                contentContainerStyle={styles.historyContent}
              >
                {showGambling && history?.spreadContext && (
                  <>
                    <Text style={[styles.historySectionTitle, { color: theme.text }]}>
                      The Line
                      {history.spreadContext.spreadDetails ? ` — ${history.spreadContext.spreadDetails}` : ''}
                    </Text>
                    {spreadContextFacts(history.spreadContext).map((f, i) => (
                      <View key={i} style={[styles.lineFact, { borderBottomColor: theme.border }]}>
                        <Text style={[styles.lineFactText, { color: theme.text }]}>
                          <Text style={styles.lineFactHead}>{f.head}</Text> {f.detail}
                        </Text>
                      </View>
                    ))}
                  </>
                )}
                {headToHead.length > 0 && (
                  <>
                    <Text style={[styles.historySectionTitle, { color: theme.text }]}>
                      Head-to-Head — Last {headToHead.length} Meeting{headToHead.length === 1 ? '' : 's'}
                    </Text>
                    {headToHead.map((g, i) => (
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

                <Text style={[styles.historySectionTitle, { color: theme.text }]}>
                  Last Season — Final {Math.max(awayPriorGames.length, homePriorGames.length)} Games
                </Text>

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
                  {categories.map((cat) => (
                    <CategoryTab
                      key={cat}
                      label={cat}
                      active={currentCategory === cat}
                      onPress={() => setActiveCategory(cat)}
                    />
                  ))}
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
                      // Use label from entry if available, else stat index
                      const label =
                        (away as any)?.label ??
                        (away as any)?.name ??
                        (home as any)?.label ??
                        (home as any)?.name ??
                        `Stat ${i + 1}`;

                      if (!away && !home) return null;

                      return (
                        <StatRow
                          key={i}
                          label={label}
                          awayEntry={away ?? { displayValue: '—' }}
                          homeEntry={home ?? { displayValue: '—' }}
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
  headerTitle: {
    fontSize: 17,
    fontWeight: '700',
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
