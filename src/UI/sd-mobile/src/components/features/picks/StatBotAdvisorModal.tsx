import React, { useEffect, useMemo, useState } from 'react';
import {
  Modal,
  View,
  FlatList,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { Text } from '@/src/components/ui/AppText';
import { SegmentedControl } from '@/src/components/ui/SegmentedControl';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { usePageSheetTopInset } from '@/src/hooks/usePageSheetTopInset';
import { usePickAdvice } from '@/src/hooks/useAdvice';
import {
  ADVISOR_LEVELS,
  applicablePicks,
  applyLabel,
  describePick,
  describeStanding,
  describeStatBot,
  kindLabel,
  levelMeta,
} from '@/src/lib/advisorSheet';
import type { AdvisedPick, AdvisorLevel, Matchup, UserPick } from '@/src/types/models';

interface Props {
  visible: boolean;
  leagueId: string;
  week: number;
  matchups: Matchup[];
  pickMap: ReadonlyMap<string, UserPick>;
  useConfidencePoints: boolean;
  applying: boolean;
  onClose: () => void;
  onApply: (picks: AdvisedPick[]) => void;
}

/**
 * StatBot advisor sheet — mobile mirror of web's StatBotAdvisorDialog. A
 * standings analysis, a risk level (StatBot's recommendation pre-selected),
 * and the full suggested sheet, with one Apply that replaces every unlocked
 * pick through the normal submit path. Read-only until Apply.
 *
 * Phone layout: the analysis and level picker scroll away as the list
 * header, the sheet is the list, and Apply stays pinned in the footer.
 * See docs/features/statbot-advisor.md.
 */
export function StatBotAdvisorModal({
  visible,
  leagueId,
  week,
  matchups,
  pickMap,
  useConfidencePoints,
  applying,
  onClose,
  onApply,
}: Props) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const topInset = usePageSheetTopInset();

  // null = "whatever StatBot recommends" (the level-less request). Choosing
  // the recommended card again returns to null so it reads the same cache
  // slot rather than fetching twice. Each level has its own query, so the
  // last click — never the last response — decides what is shown.
  const [chosen, setChosen] = useState<AdvisorLevel | null>(null);
  useEffect(() => {
    if (visible) setChosen(null);
  }, [visible]);

  const recommended = usePickAdvice(leagueId, week, null, visible);
  const chosenQuery = usePickAdvice(leagueId, week, chosen, visible && chosen != null);

  const active = chosen == null ? recommended : chosenQuery;
  const advice = active.data ?? null;
  const recommendedLevel = recommended.data?.recommendedLevel ?? null;
  const selectedLevel: AdvisorLevel | null = chosen ?? recommendedLevel;
  // Analysis is level-independent; keep it on screen while another level loads.
  const analysis = recommended.data?.analysis ?? advice?.analysis ?? null;

  const byContest = useMemo(() => new Map(matchups.map((m) => [m.contestId, m])), [matchups]);

  const teamName = (contestId: string, franchiseSeasonId?: string | null) => {
    const m = byContest.get(contestId);
    if (!m || !franchiseSeasonId) return null;
    if (franchiseSeasonId === m.homeFranchiseSeasonId) return m.homeShortName ?? m.homeShort;
    if (franchiseSeasonId === m.awayFranchiseSeasonId) return m.awayShortName ?? m.awayShort;
    return null;
  };
  const matchupLabel = (pick: AdvisedPick) => {
    const m = byContest.get(pick.contestId);
    return m ? `${m.awayShort} @ ${m.homeShort}` : (pick.headline ?? '');
  };

  const toApply = applicablePicks(advice?.picks, useConfidencePoints);
  const chooseLevel = (key: AdvisorLevel) => {
    if (applying) return;
    setChosen(key === recommendedLevel ? null : key);
  };

  const levelOptions = ADVISOR_LEVELS.map((l) => ({ value: l.key, label: l.short }));
  const statBotLine = describeStatBot(analysis);
  const errorText = active.isError
    ? "StatBot couldn't put a sheet together. Try again in a moment."
    : null;

  const header = (
    <View>
      {analysis && (
        <View style={[styles.analysis, { borderColor: theme.border, backgroundColor: theme.card }]}>
          <Text style={[styles.headline, { color: theme.text }]}>{describeStanding(analysis)}</Text>
          <View style={styles.statGrid}>
            <Stat label="Rank" value={analysis.rank != null ? `#${analysis.rank} of ${analysis.memberCount}` : '—'} theme={theme} />
            <Stat label="Pts / game" value={`${analysis.pointsPerGame}`} sub={`vs ${analysis.leaderPointsPerGame}`} theme={theme} />
            <Stat label="Total" value={`${analysis.totalPoints}`} sub={`vs ${analysis.leaderTotalPoints}`} theme={theme} />
            <Stat
              label="This week"
              value={`${analysis.gamesThisWeek} game${analysis.gamesThisWeek === 1 ? '' : 's'}`}
              sub={`max ${analysis.maxPointsThisWeek}${analysis.bestCaseRankThisWeek != null ? ` · best #${analysis.bestCaseRankThisWeek}` : ''}`}
              theme={theme}
            />
          </View>
          {statBotLine && (
            <Text style={[styles.statBotLine, { color: theme.textMuted }]}>{statBotLine}</Text>
          )}
        </View>
      )}

      <View style={styles.levels}>
        <SegmentedControl<AdvisorLevel>
          value={selectedLevel}
          options={levelOptions}
          onChange={chooseLevel}
          accessibilityLabel="Risk level"
        />
        <Text style={[styles.levelDetail, { color: theme.textMuted }]}>
          {selectedLevel && recommendedLevel === selectedLevel ? "StatBot's call · " : ''}
          {levelMeta(selectedLevel).detail}
        </Text>
      </View>

      {errorText && (
        <View style={styles.errorRow}>
          <Text style={[styles.errorText, { color: theme.warningText }]}>{errorText}</Text>
          <TouchableOpacity onPress={() => active.refetch()} hitSlop={8} disabled={applying}>
            <Text style={[styles.retry, { color: theme.tint }]}>Retry</Text>
          </TouchableOpacity>
        </View>
      )}
      {active.isLoading && !advice && (
        <View style={styles.thinking}>
          <ActivityIndicator size="small" color={theme.tint} />
          <Text style={[styles.thinkingText, { color: theme.textMuted }]}>StatBot is thinking…</Text>
        </View>
      )}
    </View>
  );

  const footerNote =
    advice && advice.noPredictionCount > 0 ? (
      <Text style={[styles.note, { color: theme.textMuted }]}>
        {advice.noPredictionCount} game{advice.noPredictionCount === 1 ? ' has' : 's have'} no
        deetsMeter number, so StatBot leaves {advice.noPredictionCount === 1 ? 'it' : 'them'} to you.
        A pick you already have there stays as it is.
      </Text>
    ) : null;

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      statusBarTranslucent
      onRequestClose={applying ? undefined : onClose}
    >
      <View style={[styles.container, { backgroundColor: theme.background, paddingTop: topInset }]}>
        <View style={[styles.header, { borderBottomColor: theme.border }]}>
          <View style={styles.titleRow}>
            <MaterialCommunityIcons name="robot" size={20} color={theme.tint} />
            <Text style={[styles.headerTitle, { color: theme.text }]}>StatBot advisor</Text>
          </View>
          <TouchableOpacity onPress={onClose} disabled={applying} hitSlop={12} accessibilityLabel="Close">
            <Text style={[styles.closeText, { color: theme.textMuted }]}>✕</Text>
          </TouchableOpacity>
        </View>

        <FlatList
          data={advice?.picks ?? []}
          keyExtractor={(p) => p.contestId}
          ListHeaderComponent={header}
          ListFooterComponent={footerNote}
          contentContainerStyle={styles.list}
          renderItem={({ item: pick }) => {
            const team = teamName(pick.contestId, pick.franchiseSeasonId);
            const changes =
              pick.differsFromExisting &&
              pick.kind !== 'Locked' &&
              pick.kind !== 'NoPrediction' &&
              !!pickMap.get(pick.contestId)?.franchiseSeasonId;
            const accent =
              pick.kind === 'Lock' ? theme.tint : pick.kind === 'Flip' ? theme.warning : theme.border;
            const dim = pick.kind === 'Locked' || pick.kind === 'NoPrediction';
            return (
              <View
                style={[styles.row, { borderBottomColor: theme.border, borderLeftColor: accent }, dim && styles.dim]}
                accessibilityLabel={`${matchupLabel(pick)}: ${team ?? 'no pick'}${
                  useConfidencePoints && pick.confidencePoints != null ? `, ${pick.confidencePoints} points` : ''
                }. ${describePick(pick)}`}
              >
                <View style={styles.rowMain}>
                  <Text style={[styles.rowMatchup, { color: theme.text }]} numberOfLines={1}>
                    {matchupLabel(pick)}
                  </Text>
                  <Text style={[styles.rowTeam, { color: theme.tint }]} numberOfLines={1}>
                    {team ?? '—'}
                  </Text>
                  {useConfidencePoints && pick.confidencePoints != null && (
                    <View style={[styles.points, { backgroundColor: theme.tint }]}>
                      <Text style={[styles.pointsText, { color: theme.textOnAccent }]}>
                        {pick.confidencePoints}
                      </Text>
                    </View>
                  )}
                </View>
                <View style={styles.rowSub}>
                  <Text style={[styles.kind, { color: theme.textMuted }]}>{kindLabel(pick.kind).toUpperCase()}</Text>
                  <Text style={[styles.reason, { color: theme.textMuted }]} numberOfLines={2}>
                    {describePick(pick)}
                  </Text>
                  {changes && (
                    <Text style={[styles.changes, { color: theme.warningText }]}>CHANGES</Text>
                  )}
                </View>
              </View>
            );
          }}
        />

        <View style={[styles.footer, { borderTopColor: theme.border }]}>
          <TouchableOpacity
            style={[styles.button, styles.cancel, { borderColor: theme.border }]}
            onPress={onClose}
            disabled={applying}
          >
            <Text style={[styles.buttonText, { color: theme.text }]}>Cancel</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.button,
              { backgroundColor: theme.tint },
              (applying || !advice || toApply.length === 0) && styles.disabled,
            ]}
            onPress={() => onApply(toApply)}
            disabled={applying || !advice || toApply.length === 0}
            accessibilityRole="button"
          >
            <Text style={[styles.buttonText, { color: theme.textOnAccent }]}>
              {applying ? 'Applying…' : applyLabel(toApply, pickMap)}
            </Text>
          </TouchableOpacity>
        </View>

        {applying && (
          <View style={styles.savingOverlay}>
            <ActivityIndicator size="small" color={theme.tint} />
          </View>
        )}
      </View>
    </Modal>
  );
}

function Stat({
  label,
  value,
  sub,
  theme,
}: {
  label: string;
  value: string;
  sub?: string;
  theme: ReturnType<typeof getTheme>;
}) {
  return (
    <View style={styles.stat}>
      <Text style={[styles.statLabel, { color: theme.textMuted }]}>{label}</Text>
      <Text style={[styles.statValue, { color: theme.text }]} numberOfLines={1}>
        {value}
      </Text>
      {sub ? (
        <Text style={[styles.statSub, { color: theme.textMuted }]} numberOfLines={1}>
          {sub}
        </Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  titleRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  headerTitle: { fontSize: 18, fontWeight: '700' },
  closeText: { fontSize: 20, fontWeight: '600' },
  list: { paddingHorizontal: 16, paddingBottom: 16 },
  analysis: {
    marginTop: 12,
    padding: 12,
    borderRadius: 12,
    borderWidth: StyleSheet.hairlineWidth,
  },
  headline: { fontSize: 14, lineHeight: 20, fontWeight: '600', marginBottom: 10 },
  statGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  stat: { width: '47%', flexGrow: 1 },
  statLabel: { fontSize: 11, textTransform: 'uppercase', letterSpacing: 0.5 },
  statValue: { fontSize: 15, fontWeight: '700', marginTop: 2 },
  statSub: { fontSize: 12 },
  statBotLine: { marginTop: 10, fontSize: 12, fontStyle: 'italic' },
  levels: { paddingTop: 12, gap: 6 },
  levelDetail: { fontSize: 12, lineHeight: 16 },
  errorRow: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 10 },
  errorText: { flex: 1, fontSize: 13 },
  retry: { fontSize: 13, fontWeight: '700' },
  thinking: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingVertical: 16 },
  thinkingText: { fontSize: 13 },
  row: {
    paddingVertical: 10,
    paddingLeft: 10,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderLeftWidth: 3,
  },
  dim: { opacity: 0.7 },
  rowMain: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  rowMatchup: { flex: 1, fontSize: 14 },
  rowTeam: { fontSize: 14, fontWeight: '700', maxWidth: 140 },
  points: {
    minWidth: 26,
    paddingHorizontal: 6,
    paddingVertical: 1,
    borderRadius: 999,
    alignItems: 'center',
  },
  pointsText: { fontSize: 12, fontWeight: '700' },
  rowSub: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 2 },
  kind: { fontSize: 10, fontWeight: '700', letterSpacing: 0.5 },
  reason: { flex: 1, fontSize: 12 },
  changes: { fontSize: 10, fontWeight: '700', letterSpacing: 0.5 },
  note: { fontSize: 12, lineHeight: 16, paddingTop: 10 },
  footer: {
    flexDirection: 'row',
    gap: 12,
    padding: 16,
    borderTopWidth: StyleSheet.hairlineWidth,
  },
  button: {
    flex: 1,
    paddingVertical: 13,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
  },
  cancel: { borderWidth: 1 },
  disabled: { opacity: 0.5 },
  buttonText: { fontSize: 15, fontWeight: '700' },
  savingOverlay: {
    ...StyleSheet.absoluteFillObject,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(0,0,0,0.15)',
  },
});
