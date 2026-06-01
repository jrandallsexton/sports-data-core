import React from 'react';
import { View, StyleSheet } from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import type { PickType } from '@/src/types/models';

// Quick-scan result row appended below the final score for ATS / O/U
// leagues. SU is NOT rendered here — its checkmark is inline next to
// the winning team's short directly in GameStatus.tsx's STATUS_FINAL
// branch (the score line already shows the winning team, so a duplicated
// "NYY won" suffix would be noise).
//
// Pure presentation — does NOT reflect the user's own pick (PickButton
// handles that). The goal is letting a user glance at a finalized card
// and confirm the result relevant to the league's pick mode without
// parsing the score themselves.
//
// Readiness contract: when status=STATUS_FINAL flips, Contest enrichment
// (PR #384) runs with a ~30s delay. During that window the wire still
// reports STATUS_FINAL but winnerFranchiseSeasonId / spreadWinnerFranchise
// SeasonId / overUnderResult are all null. We stay silent in that gap
// rather than rendering a false "Push".

interface FinalScoreResultProps {
  pickType?: PickType | null;
  awayFranchiseSeasonId?: string | null;
  homeFranchiseSeasonId?: string | null;
  awayShort?: string;
  homeShort?: string;
  winnerFranchiseSeasonId?: string | null;
  spreadWinnerFranchiseSeasonId?: string | null;
  overUnderResult?: string | number | null;
  overUnderCurrent?: number | null;
}

export function FinalScoreResult({
  pickType,
  awayFranchiseSeasonId,
  homeFranchiseSeasonId,
  awayShort,
  homeShort,
  winnerFranchiseSeasonId,
  spreadWinnerFranchiseSeasonId,
  overUnderResult,
  overUnderCurrent,
}: FinalScoreResultProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const shortFor = (franchiseSeasonId: string | null | undefined): string | null => {
    if (!franchiseSeasonId) return null;
    if (franchiseSeasonId === awayFranchiseSeasonId) return awayShort ?? null;
    if (franchiseSeasonId === homeFranchiseSeasonId) return homeShort ?? null;
    return null;
  };

  if (pickType === 'AgainstTheSpread') {
    // Gate on winnerFranchiseSeasonId as the "enrichment ran" signal.
    // Pre-enrichment all three result fields are null on the wire;
    // treating a null spread winner as "Push" then would lie about a
    // game that hasn't been scored yet. We accept missing the ATS
    // indicator on a true tie (rare in football, impossible in MLB)
    // rather than showing a false push during the enrichment window.
    if (winnerFranchiseSeasonId == null) return null;

    // Enrichment ran. A null spread winner now genuinely means push at
    // the spread (game landed exactly on the line).
    if (spreadWinnerFranchiseSeasonId == null) {
      return (
        <View style={styles.row}>
          <Text style={[styles.pushText, { color: theme.textMuted }]}>Push</Text>
        </View>
      );
    }
    const cover = shortFor(spreadWinnerFranchiseSeasonId);
    if (!cover) return null;
    return (
      <View style={styles.row}>
        <Text style={[styles.checkmark, { color: '#16A34A' }]}>✓</Text>
        <Text style={[styles.resultText, { color: theme.textMuted }]}>
          {cover} covered
        </Text>
      </View>
    );
  }

  if (pickType === 'OverUnder') {
    // Only render when we recognize an explicit O/U verdict. Anything
    // else (null / undefined / "None") means enrichment hasn't computed
    // a result and we stay silent rather than showing a false "Push".
    //
    // Wire-shape note: API DTO uses OverUnderPick (None/Over/Under) but
    // Producer's Contest.OverUnder uses OverUnderResult which has Push=3.
    // MatchupForPickDtoMapper casts the int through, so a real push can
    // arrive as "Push", 3, or "3" depending on the serializer's behavior
    // on the unnamed enum value. Match all three; the contract mismatch
    // itself is a separate change.
    const isOver = overUnderResult === 'Over';
    const isUnder = overUnderResult === 'Under';
    const isPush =
      overUnderResult === 'Push' ||
      overUnderResult === 3 ||
      overUnderResult === '3';

    if (isOver || isUnder) {
      const ouValue =
        overUnderCurrent !== null && overUnderCurrent !== undefined
          ? ` ${overUnderCurrent}`
          : '';
      return (
        <View style={styles.row}>
          <Text style={[styles.checkmark, { color: '#16A34A' }]}>✓</Text>
          <Text style={[styles.resultText, { color: theme.textMuted }]}>
            {isOver ? 'Over' : 'Under'}
            {ouValue}
          </Text>
        </View>
      );
    }

    if (isPush) {
      return (
        <View style={styles.row}>
          <Text style={[styles.pushText, { color: theme.textMuted }]}>Push</Text>
        </View>
      );
    }

    return null;
  }

  // StraightUp (and unknown pickType) — no row, handled inline by the
  // score line in GameStatus.tsx.
  return null;
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    marginTop: 2,
  },
  checkmark: {
    fontSize: 13,
    fontWeight: '700',
  },
  resultText: {
    fontSize: 13,
    fontWeight: '600',
    letterSpacing: 0.3,
  },
  pushText: {
    fontSize: 13,
    fontWeight: '600',
    fontStyle: 'italic',
    letterSpacing: 1.0,
  },
});
