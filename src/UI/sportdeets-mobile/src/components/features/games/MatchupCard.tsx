import React, { useState } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Image } from 'react-native';
import { useColorScheme } from 'react-native';
import { Colors, getTheme } from '@/constants/Colors';
import type { Matchup, UserPick, PickChoice, PreviewResponse, TeamComparisonData } from '@/src/types/models';
import { matchupsApi } from '@/src/services/api/matchupsApi';
import { teamCardApi } from '@/src/services/api/teamCardApi';
import { InsightModal } from './InsightModal';
import { StatsComparisonModal } from './StatsComparisonModal';

// ─── Helpers ──────────────────────────────────────────────────────────────────

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

function spreadLabel(spread: number | null): string {
  if (spread === null) return '';
  if (spread === 0) return 'PK';
  return spread > 0 ? `+${spread}` : `${spread}`;
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
      </View>

      {/* Score + pick indicator */}
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
    </View>
  );
}

// ─── Spread & O/U row ─────────────────────────────────────────────────────────

function OddsRow({ matchup }: { matchup: Matchup }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const spread = matchup.spreadCurrent;
  const ou = matchup.overUnderCurrent;
  if (spread == null && ou == null) return null;

  return (
    <View style={[styles.oddsRow, { borderColor: theme.separator }]}>
      {spread != null && (
        <View style={styles.oddsItem}>
          <Text style={[styles.oddsLabel, { color: theme.textMuted }]}>Spread</Text>
          <Text style={[styles.oddsValue, { color: theme.tint }]}>{spreadLabel(spread)}</Text>
        </View>
      )}
      {spread != null && ou != null && (
        <View style={[styles.oddsSep, { backgroundColor: theme.separator }]} />
      )}
      {ou != null && (
        <View style={styles.oddsItem}>
          <Text style={[styles.oddsLabel, { color: theme.textMuted }]}>O/U</Text>
          <Text style={[styles.oddsValue, { color: theme.tint }]}>{ou}</Text>
        </View>
      )}
    </View>
  );
}

// ─── Status section ───────────────────────────────────────────────────────────

function StatusSection({ matchup }: { matchup: Matchup }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const status = matchup.status.toLowerCase();

  if (status === 'scheduled') {
    return (
      <View style={styles.statusSection}>
        <Text style={[styles.statusTime, { color: theme.text }]}>{formatTime(matchup.startDateUtc)}</Text>
        {matchup.broadcasts && (
          <Text style={[styles.statusMeta, { color: theme.textMuted }]}>{matchup.broadcasts}</Text>
        )}
        {matchup.venue && (
          <Text style={[styles.statusMeta, { color: theme.textMuted }]} numberOfLines={1}>
            {[matchup.venue, matchup.venueCity, matchup.venueState].filter(Boolean).join(', ')}
          </Text>
        )}
      </View>
    );
  }

  if (status === 'inprogress' || status === 'ongoing' || status === 'halftime') {
    return (
      <View style={styles.statusSection}>
        <View style={styles.liveRow}>
          <View style={styles.liveDot} />
          <Text style={styles.liveText}>LIVE</Text>
        </View>
      </View>
    );
  }

  if (status === 'final' || status === 'completed') {
    return (
      <View style={styles.statusSection}>
        <Text style={[styles.statusLabel, { color: theme.textMuted }]}>Final</Text>
      </View>
    );
  }

  // postponed, cancelled, etc.
  return (
    <View style={styles.statusSection}>
      <Text style={[styles.statusLabel, { color: theme.error }]}>{matchup.status}</Text>
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
          {matchup.isPreviewAvailable ? '📈' : '🔒'}
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
  onPress?: () => void;
  onPick?: (matchup: Matchup, choice: PickChoice, franchiseSeasonId: string) => void;
  /** Season year used for team stats API calls. Defaults to the game start year. */
  seasonYear?: number;
}

export function MatchupCard({ matchup, pick, onPress, onPick, seasonYear }: MatchupCardProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const status = matchup.status.toLowerCase();
  const isFinal = status === 'final' || status === 'completed';

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
    matchup.homeScore != null &&
    matchup.awayScore != null &&
    matchup.homeScore >= matchup.awayScore;

  // Use server pick if available, otherwise optimistic
  const effectiveFranchiseId = pick?.franchiseId ?? optimisticFranchiseId;

  const pickedHome = effectiveFranchiseId === matchup.homeFranchiseSeasonId;
  const pickedAway = effectiveFranchiseId === matchup.awayFranchiseSeasonId;
  const hasPick = pickedHome || pickedAway;

  // Pick result
  const isPickCorrect = isFinal && hasPick ? (pick?.isCorrect ?? null) : null;

  const locked = isPickLocked(matchup);

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
    <TouchableOpacity
      style={[
        styles.card,
        { backgroundColor: theme.card, borderColor: cardBorderColor },
        isFinal && hasPick && isPickCorrect === true && styles.cardCorrect,
        isFinal && (isPickCorrect === false || !hasPick) && styles.cardIncorrect,
      ]}
      onPress={onPress}
      activeOpacity={onPress ? 0.75 : 1}
      disabled={!onPress}
    >
      {/* Headline banner */}
      {matchup.headLine != null && matchup.headLine !== '' && (
        <View style={styles.headline}>
          <Text style={styles.headlineText} numberOfLines={1}>{matchup.headLine}</Text>
        </View>
      )}

      {/* Away team */}
      <TeamRow
        matchup={matchup}
        side="away"
        isWinning={!homeIsWinning}
        isPicked={pickedAway}
        isPickCorrect={isPickCorrect}
        isFinal={isFinal}
      />

      {/* Status */}
      <StatusSection matchup={matchup} />

      {/* Home team */}
      <TeamRow
        matchup={matchup}
        side="home"
        isWinning={homeIsWinning}
        isPicked={pickedHome}
        isPickCorrect={isPickCorrect}
        isFinal={isFinal}
      />

      {/* Spread & O/U */}
      <OddsRow matchup={matchup} />

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
    </TouchableOpacity>

    {/* Modals — rendered outside TouchableOpacity so they overlay in full screen */}
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

  // Status section
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

  // Odds row
  oddsRow: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    borderTopWidth: StyleSheet.hairlineWidth,
    paddingVertical: 8,
    paddingHorizontal: 14,
    gap: 16,
  },
  oddsItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  oddsLabel: {
    fontSize: 11,
    fontWeight: '600',
    textTransform: 'uppercase',
    letterSpacing: 0.3,
  },
  oddsValue: {
    fontSize: 14,
    fontWeight: '700',
  },
  oddsSep: {
    width: 1,
    height: 16,
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
    borderWidth: 1.5,
    borderRadius: 10,
    paddingVertical: 10,
    alignItems: 'center',
    gap: 2,
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
