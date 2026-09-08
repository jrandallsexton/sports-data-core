import React, { useEffect, useMemo, useState } from 'react';
import {
  View,
  FlatList,
  TouchableOpacity,
  StyleSheet,
  RefreshControl,
  LayoutAnimation,
  Platform,
  UIManager,
  ScrollView,
} from 'react-native';
import { useIsFocused } from '@react-navigation/native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { EmptyState } from '@/src/components/ui/EmptyState';
import { Button } from '@/src/components/ui/Button';
import { useLeagueWeekOverview } from '@/src/hooks/useLeagueWeekOverview';
import { useUserOptions } from '@/src/hooks/useUserOptions';
import { shouldShowGambling } from '@/src/lib/gamblingContent';
import {
  contestPhase,
  indexPicks,
  memberRowsForContest,
  readinessRows,
  sideSplit,
  summarizeWeek,
  type ContestPhase,
  type MemberPickRow,
} from '@/src/lib/weekOverview';
import type { PickType } from '@/src/types/models';
import type { LeagueWeekContest, LeagueWeekMember, UserPick } from '@/src/types/models';

// The "By Week" pane (design handoff: D consensus rail as the pane, A's
// member rows as each strip's expanded state, A3 pre-lock readiness, A4
// spreads-off). One FlatList; expansion is per-item state — no nested
// scrolling.

if (Platform.OS === 'android') {
  UIManager.setLayoutAnimationEnabledExperimental?.(true);
}

const CORRECT = '#34d399';
const INCORRECT = '#f87171';

interface ByWeekPaneProps {
  leagueId: string;
  week: number | null;
  seasonWeeks: number[];
  onWeekChange: (week: number) => void;
  showBots: boolean;
  currentUserId: string | undefined;
  pickType: PickType | null;
}

export function ByWeekPane({
  leagueId,
  week,
  seasonWeeks,
  onWeekChange,
  showBots,
  currentUserId,
  pickType,
}: ByWeekPaneProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const { data: userOptions } = useUserOptions();
  const showGambling = shouldShowGambling(pickType, userOptions);

  // Poll only while FOCUSED (tab screens stay mounted) — the hook stops on
  // its own once every game is final.
  const isFocused = useIsFocused();
  const { data: overview, isLoading, isError, refetch, isRefetching } =
    useLeagueWeekOverview(leagueId, week, isFocused);

  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const toggleExpanded = (contestId: string) => {
    LayoutAnimation.configureNext(LayoutAnimation.Presets.easeInEaseOut);
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(contestId)) next.delete(contestId);
      else next.add(contestId);
      return next;
    });
  };

  // 15s clock tick while focused (picks.tsx precedent): time-only derivations
  // — the LOCKED→LIVE flip at kickoff, the pre-lock countdown — advance
  // between refetches instead of freezing at the last render's timestamp.
  const [nowMs, setNowMs] = useState(() => Date.now());
  useEffect(() => {
    if (!isFocused) return undefined;
    setNowMs(Date.now());
    const id = setInterval(() => setNowMs(Date.now()), 15_000);
    return () => clearInterval(id);
  }, [isFocused]);

  const derived = useMemo(() => {
    if (!overview) return null;
    const members = showBots
      ? overview.members
      : overview.members.filter((m) => !m.isSynthetic);
    const memberIds = new Set(members.map((m) => m.userId));
    const picks = overview.userPicks.filter((p) => memberIds.has(p.userId));
    return {
      members,
      picksByUser: indexPicks(picks),
      summary: summarizeWeek(overview, nowMs),
    };
  }, [overview, showBots, nowMs]);

  const weekChips = (
    <View style={styles.weekRow}>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.weekRowContent}>
        {seasonWeeks.map((w) => {
          const active = w === week;
          return (
            <TouchableOpacity
              key={w}
              style={[
                styles.weekChip,
                active
                  ? { backgroundColor: theme.tint, borderColor: theme.tint }
                  : { borderColor: theme.border },
              ]}
              onPress={() => onWeekChange(w)}
              accessibilityRole="button"
              accessibilityState={{ selected: active }}
              activeOpacity={0.7}
            >
              <Text style={[styles.weekChipText, { color: active ? theme.textOnAccent : theme.textMuted }]}>
                Wk {w}
              </Text>
            </TouchableOpacity>
          );
        })}
      </ScrollView>
    </View>
  );

  // A week-less league (slate not generated yet, or a future season whose
  // weeks aren't sourced) reaches here with week == null: the query is
  // disabled, so isLoading never resolves and isError never fires — without
  // this branch the pane would spin forever with no escape.
  if (week == null) {
    return (
      <View style={styles.fill}>
        <EmptyState
          icon="📅"
          title="No weeks yet"
          subtitle="This league's schedule hasn't been generated yet — check back soon."
        />
      </View>
    );
  }

  if (isLoading || !derived) {
    return (
      <View style={styles.fill}>
        {weekChips}
        {isError ? (
          <View style={styles.errorBox}>
            <Text style={[styles.errorTitle, { color: theme.text }]}>Couldn't load the week</Text>
            <Button title="Retry" variant="secondary" size="sm" onPress={() => refetch()} />
          </View>
        ) : (
          <LoadingSpinner message="Loading week…" />
        )}
      </View>
    );
  }

  const { members, picksByUser, summary } = derived;

  // ── A3: nothing revealed yet ──────────────────────────────────────────────
  if (summary.preLock) {
    const rows = readinessRows(members, overview!.contests.length);
    return (
      <View style={styles.fill}>
        {weekChips}
        <FlatList
          data={rows}
          keyExtractor={(r) => r.member.userId}
          contentContainerStyle={styles.list}
          refreshControl={
            <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={theme.tint} />
          }
          ListHeaderComponent={
            <View style={[styles.revealCard, { backgroundColor: theme.card, borderColor: theme.border }]}>
              <Text style={styles.revealLock}>🔒</Text>
              <Text style={[styles.revealTitle, { color: theme.text }]}>Picks reveal at kickoff</Text>
              <Text style={[styles.revealSub, { color: theme.textMuted }]}>
                Nobody sees anybody's picks until a game locks — 5 minutes before kick.
              </Text>
              {summary.firstLockMs != null && (
                <View style={[styles.revealWhen, { borderColor: theme.tint }]}>
                  <Text style={[styles.revealWhenText, { color: theme.tint }]}>
                    First lock {formatLockTime(summary.firstLockMs)}
                  </Text>
                </View>
              )}
              <Text style={[styles.readyHeader, { color: theme.textMuted }]}>
                WHO'S READY · {overview!.contests.length} GAMES
              </Text>
            </View>
          }
          renderItem={({ item }) => {
            const isMe = item.member.userId === currentUserId;
            const share = item.total === 0 ? 0 : item.submitted / item.total;
            return (
              <View
                style={[
                  styles.readyRow,
                  { backgroundColor: theme.card, borderColor: theme.border },
                  isMe && { borderLeftColor: theme.tint, borderLeftWidth: 3 },
                ]}
              >
                <Text style={[styles.readyName, { color: theme.text }]} numberOfLines={1}>
                  {item.member.displayName}
                  {isMe ? ' (you)' : ''}
                </Text>
                <View style={[styles.readyBar, { backgroundColor: theme.border }]}>
                  <View
                    style={[
                      styles.readyBarFill,
                      { backgroundColor: share >= 1 ? CORRECT : theme.tint, flex: Math.max(share, 0.02) },
                    ]}
                  />
                  <View style={{ flex: Math.max(1 - share, 0.001) }} />
                </View>
                <Text style={[styles.readyCount, { color: share >= 1 ? CORRECT : theme.tint }]}>
                  {item.submitted}/{item.total}
                </Text>
              </View>
            );
          }}
          ItemSeparatorComponent={() => <View style={{ height: 6 }} />}
        />
      </View>
    );
  }

  // ── D: consensus rail over revealed games ────────────────────────────────
  return (
    <View style={styles.fill}>
      {weekChips}
      <FlatList
        data={summary.revealed}
        keyExtractor={(c) => c.contestId}
        contentContainerStyle={styles.list}
        showsVerticalScrollIndicator={false}
        refreshControl={
          <RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={theme.tint} />
        }
        renderItem={({ item }) => (
          <GameStrip
            contest={item}
            phase={contestPhase(item, nowMs)}
            members={members}
            picksByUser={picksByUser}
            currentUserId={currentUserId}
            showGambling={showGambling}
            expanded={expanded.has(item.contestId)}
            onToggle={() => toggleExpanded(item.contestId)}
          />
        )}
        ItemSeparatorComponent={() => <View style={{ height: 8 }} />}
        ListEmptyComponent={
          <EmptyState title="No games this week" subtitle="This week has no matchups." />
        }
        ListFooterComponent={
          summary.unlockedCount > 0 ? (
            <Text style={[styles.footerNote, { color: theme.textMuted }]}>
              🔒 {summary.unlockedCount} more {summary.unlockedCount === 1 ? 'game' : 'games'} —
              picks reveal 5 min before kickoff
            </Text>
          ) : null
        }
      />
    </View>
  );
}

// ─── One rail strip (collapsed = D, expanded = A's member rows) ──────────────

function GameStrip({
  contest,
  phase,
  members,
  picksByUser,
  currentUserId,
  showGambling,
  expanded,
  onToggle,
}: {
  contest: LeagueWeekContest;
  phase: ContestPhase;
  members: LeagueWeekMember[];
  picksByUser: Map<string, Map<string, UserPick>>;
  currentUserId: string | undefined;
  showGambling: boolean;
  expanded: boolean;
  onToggle: () => void;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const rows = useMemo(
    () => memberRowsForContest(contest, members, picksByUser),
    [contest, members, picksByUser],
  );
  const revealedPicks = rows.filter((r): r is Required<MemberPickRow> => !!r.pick).map((r) => r.pick);
  const split = sideSplit(contest, revealedPicks);
  const noPickCount = rows.length - revealedPicks.length;

  const hasScore = contest.awayScore != null && contest.homeScore != null;
  const scoreText = hasScore ? `${contest.awayScore}–${contest.homeScore}` : kickTime(contest.startDateUtc);

  return (
    <TouchableOpacity
      style={[styles.strip, { backgroundColor: theme.card, borderColor: theme.border }]}
      onPress={onToggle}
      activeOpacity={0.85}
      accessibilityRole="button"
      accessibilityState={{ expanded }}
    >
      {/* Header: matchup · score/kick · status */}
      <View style={styles.stripHeader}>
        <Text style={[styles.matchup, { color: theme.text }]} numberOfLines={1}>
          {rankPrefix(contest.awayRank)}
          {contest.awayShort} @ {rankPrefix(contest.homeRank)}
          {contest.homeShort}
        </Text>
        <Text style={[styles.score, { color: phase === 'live' ? CORRECT : theme.text }]}>{scoreText}</Text>
        <StatusPill phase={phase} />
      </View>

      {showGambling && contest.homeSpread != null && contest.homeSpread !== 0 && (
        <Text style={[styles.spread, { color: theme.textMuted }]}>
          {contest.homeShort} {contest.homeSpread > 0 ? '+' : ''}
          {contest.homeSpread}
        </Text>
      )}

      {/* Majority split bar */}
      <View style={styles.splitRow}>
        <Text style={[styles.splitLabel, { color: theme.tint }]}>
          {split.awayCount} {contest.awayShort}
        </Text>
        <View style={[styles.splitBar, { backgroundColor: theme.border }]}>
          <View style={{ flex: Math.max(split.awayShare, 0.03), backgroundColor: theme.tint }} />
          <View style={{ flex: Math.max(1 - split.awayShare, 0.03) }} />
        </View>
        <Text style={[styles.splitLabel, { color: theme.textMuted }]}>
          {contest.homeShort} {split.homeCount}
        </Text>
      </View>

      {/* Collapsed: name pills. Expanded: full member rows. */}
      {expanded ? (
        <View style={styles.memberRows}>
          {rows.map((row) => (
            <MemberRow key={row.member.userId} row={row} isMe={row.member.userId === currentUserId} />
          ))}
        </View>
      ) : (
        <View style={styles.pillWrap}>
          {rows
            .filter((r) => r.pick)
            .map((row) => (
              <NamePill key={row.member.userId} row={row} isMe={row.member.userId === currentUserId} />
            ))}
          {noPickCount > 0 && (
            <Text style={[styles.noPickTail, { color: theme.textMuted }]}>
              {noPickCount} no pick 🔒
            </Text>
          )}
        </View>
      )}
    </TouchableOpacity>
  );
}

function StatusPill({ phase }: { phase: ContestPhase }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const label = phase === 'final' ? 'FINAL' : phase === 'live' ? 'LIVE' : 'LOCKED';
  const color = phase === 'live' ? CORRECT : theme.textMuted;
  return (
    <View style={[styles.statusPill, { borderColor: color }]}>
      <Text style={[styles.statusPillText, { color }]}>{label}</Text>
    </View>
  );
}

function ConfidenceBadge({ pick }: { pick: UserPick }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  if (pick.confidencePoints == null) return null;
  const color =
    pick.isCorrect === true ? CORRECT : pick.isCorrect === false ? INCORRECT : theme.tint;
  return (
    <View style={[styles.confBadge, { borderColor: color }]}>
      <Text style={[styles.confBadgeText, { color }]}>{pick.confidencePoints}</Text>
    </View>
  );
}

function ResultMark({ pick }: { pick: UserPick }) {
  if (typeof pick.isCorrect !== 'boolean') return null;
  return (
    <Text style={[styles.mark, { color: pick.isCorrect ? CORRECT : INCORRECT }]}>
      {pick.isCorrect ? '✓' : '✗'}
    </Text>
  );
}

function NamePill({ row, isMe }: { row: MemberPickRow; isMe: boolean }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const firstName = isMe ? 'You' : row.member.displayName.split(' ')[0];
  return (
    <View
      style={[
        styles.pill,
        { borderColor: isMe ? theme.tint : theme.border, backgroundColor: theme.background },
      ]}
    >
      {row.pick && <ConfidenceBadge pick={row.pick} />}
      <Text style={[styles.pillName, { color: theme.text, fontWeight: isMe ? '700' : '500' }]} numberOfLines={1}>
        {firstName}
      </Text>
      {row.pick && <ResultMark pick={row.pick} />}
    </View>
  );
}

function MemberRow({ row, isMe }: { row: MemberPickRow; isMe: boolean }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  return (
    <View
      style={[
        styles.memberRow,
        isMe && { backgroundColor: `${theme.tint}14`, borderLeftColor: theme.tint, borderLeftWidth: 3 },
      ]}
    >
      <Text
        style={[
          styles.memberName,
          { color: row.pick ? theme.text : theme.textMuted, fontWeight: isMe ? '700' : '500' },
        ]}
        numberOfLines={1}
      >
        {row.member.displayName}
        {isMe ? ' (you)' : ''}
      </Text>
      {row.pick ? (
        <>
          <View
            style={[
              styles.teamChip,
              {
                borderColor:
                  row.pick.isCorrect === true
                    ? CORRECT
                    : row.pick.isCorrect === false
                      ? INCORRECT
                      : theme.border,
              },
            ]}
          >
            <Text style={[styles.teamChipText, { color: theme.text }]}>{row.teamShort ?? '—'}</Text>
          </View>
          <ConfidenceBadge pick={row.pick} />
          <ResultMark pick={row.pick} />
        </>
      ) : (
        <View style={[styles.teamChip, styles.noPickChip, { borderColor: theme.border }]}>
          <Text>🔒</Text>
        </View>
      )}
    </View>
  );
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function rankPrefix(rank: number | null | undefined): string {
  return rank != null ? `#${rank} ` : '';
}

function kickTime(startDateUtc: string): string {
  const d = new Date(startDateUtc);
  return d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
}

function formatLockTime(lockMs: number): string {
  const d = new Date(lockMs);
  const day = d.toLocaleDateString(undefined, { weekday: 'short' });
  const time = d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  const deltaMs = lockMs - Date.now();
  if (deltaMs <= 0) return `${day} ${time}`;
  const hours = Math.floor(deltaMs / 3_600_000);
  const days = Math.floor(hours / 24);
  const remainder = days > 0 ? `${days}d ${hours % 24}h` : `${hours}h ${Math.floor((deltaMs % 3_600_000) / 60_000)}m`;
  return `${day} ${time} · ${remainder}`;
}

// ─── Styles ──────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  fill: { flex: 1 },
  list: { padding: 14, paddingBottom: 24 },
  weekRow: { paddingTop: 8 },
  weekRowContent: { paddingHorizontal: 14, gap: 6 },
  weekChip: {
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  weekChipText: { fontSize: 12, fontWeight: '600' },
  errorBox: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 10, padding: 30 },
  errorTitle: { fontSize: 16, fontWeight: '700' },

  // A3 pre-lock
  revealCard: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 20,
    alignItems: 'center',
    gap: 8,
    marginBottom: 12,
  },
  revealLock: { fontSize: 34 },
  revealTitle: { fontSize: 17, fontWeight: '800', textTransform: 'uppercase', letterSpacing: 0.5 },
  revealSub: { fontSize: 13, textAlign: 'center', lineHeight: 19 },
  revealWhen: { borderWidth: 1, borderRadius: 8, paddingHorizontal: 12, paddingVertical: 6, marginTop: 4 },
  revealWhenText: { fontSize: 13, fontWeight: '600', fontVariant: ['tabular-nums'] },
  readyHeader: { fontSize: 11, fontWeight: '700', letterSpacing: 1, marginTop: 10, alignSelf: 'flex-start' },
  readyRow: {
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: 10,
    borderWidth: StyleSheet.hairlineWidth,
    paddingHorizontal: 14,
    paddingVertical: 12,
    gap: 10,
  },
  readyName: { flex: 1, fontSize: 14, fontWeight: '600' },
  readyBar: { flexDirection: 'row', width: 90, height: 6, borderRadius: 3, overflow: 'hidden' },
  readyBarFill: { borderRadius: 3 },
  readyCount: { fontSize: 13, fontWeight: '700', fontVariant: ['tabular-nums'], minWidth: 42, textAlign: 'right' },

  // D strip
  strip: {
    borderRadius: 13,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 12,
    gap: 8,
  },
  stripHeader: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  matchup: { flex: 1, fontSize: 15, fontWeight: '800' },
  score: { fontSize: 15, fontWeight: '700', fontVariant: ['tabular-nums'] },
  statusPill: { borderWidth: 1, borderRadius: 5, paddingHorizontal: 6, paddingVertical: 2 },
  statusPillText: { fontSize: 10, fontWeight: '700', letterSpacing: 0.5 },
  spread: { fontSize: 12, fontVariant: ['tabular-nums'], marginTop: -4 },
  splitRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  splitLabel: { fontSize: 12, fontWeight: '700', minWidth: 52 },
  splitBar: { flex: 1, flexDirection: 'row', height: 6, borderRadius: 3, overflow: 'hidden' },
  pillWrap: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, alignItems: 'center' },
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  pillName: { fontSize: 12, maxWidth: 90 },
  noPickTail: { fontSize: 12, marginLeft: 4 },
  footerNote: { fontSize: 12, textAlign: 'center', paddingVertical: 14 },

  // A expanded rows
  memberRows: { gap: 2 },
  memberRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingVertical: 8,
    paddingHorizontal: 6,
    borderRadius: 9,
    minHeight: 38,
  },
  memberName: { flex: 1, fontSize: 14 },
  teamChip: {
    width: 56,
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: 6,
    paddingVertical: 4,
  },
  noPickChip: { borderStyle: 'dashed' },
  teamChipText: { fontSize: 12, fontWeight: '800' },
  confBadge: {
    width: 20,
    height: 20,
    borderRadius: 10,
    borderWidth: 1.5,
    alignItems: 'center',
    justifyContent: 'center',
  },
  confBadgeText: { fontSize: 11, fontWeight: '700' },
  mark: { fontSize: 14, fontWeight: '700' },
});
