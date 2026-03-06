import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
} from 'react-native';
import { useLocalSearchParams, useNavigation } from 'expo-router';
import { useColorScheme } from 'react-native';
import { Colors, getTheme } from '@/constants/Colors';
import { MatchupCard } from '@/src/components/features/games/MatchupCard';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { useMatchups } from '@/src/hooks/useMatchups';
import { usePicks, useSubmitPick } from '@/src/hooks/useContest';
import type { PickChoice } from '@/src/types/models';

// ─── Pick selector ────────────────────────────────────────────────────────────

function PickSelector({
  homeTeamName,
  awayTeamName,
  homeFranchiseSeasonId,
  awayFranchiseSeasonId,
  existingPickFranchiseId,
  isLocked,
  onPick,
}: {
  homeTeamName: string;
  awayTeamName: string;
  homeFranchiseSeasonId: string;
  awayFranchiseSeasonId: string;
  existingPickFranchiseId: string | null;
  isLocked: boolean;
  onPick: (choice: PickChoice, franchiseSeasonId: string) => void;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const pickedHome = existingPickFranchiseId === homeFranchiseSeasonId;
  const pickedAway = existingPickFranchiseId === awayFranchiseSeasonId;
  const hasPick = pickedHome || pickedAway;
  const locked = isLocked || hasPick;

  return (
    <View style={[styles.pickSection, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <Text style={[styles.pickTitle, { color: theme.text }]}>
        {isLocked ? '🔒 Pick Locked' : hasPick ? '✓ Pick Submitted' : 'Make Your Pick'}
      </Text>
      {isLocked && (
        <Text style={[styles.pickSubtitle, { color: theme.textMuted }]}>
          Game has started — picks are no longer accepted.
        </Text>
      )}
      <View style={styles.pickButtons}>
        <TouchableOpacity
          style={[
            styles.pickBtn,
            { borderColor: theme.border, backgroundColor: theme.background },
            pickedAway && styles.pickBtnSelected,
          ]}
          onPress={() => !locked && onPick('away', awayFranchiseSeasonId)}
          disabled={locked}
          activeOpacity={locked ? 1 : 0.75}
        >
          <Text style={[styles.pickBtnLabel, { color: theme.textMuted }]}>Away</Text>
          <Text
            style={[
              styles.pickBtnTeam,
              { color: pickedAway ? Colors.brand.navy : theme.text },
            ]}
            numberOfLines={2}
          >
            {awayTeamName}
          </Text>
        </TouchableOpacity>

        <View style={[styles.vsBox, { backgroundColor: theme.separator }]}>
          <Text style={[styles.vsText, { color: theme.textMuted }]}>@</Text>
        </View>

        <TouchableOpacity
          style={[
            styles.pickBtn,
            { borderColor: theme.border, backgroundColor: theme.background },
            pickedHome && styles.pickBtnSelected,
          ]}
          onPress={() => !locked && onPick('home', homeFranchiseSeasonId)}
          disabled={locked}
          activeOpacity={locked ? 1 : 0.75}
        >
          <Text style={[styles.pickBtnLabel, { color: theme.textMuted }]}>Home</Text>
          <Text
            style={[
              styles.pickBtnTeam,
              { color: pickedHome ? Colors.brand.navy : theme.text },
            ]}
            numberOfLines={2}
          >
            {homeTeamName}
          </Text>
        </TouchableOpacity>
      </View>
    </View>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function GameDetailScreen() {
  const { id, leagueId, week: weekParam } = useLocalSearchParams<{
    id: string;
    leagueId?: string;
    week?: string;
  }>();
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const weekNumber = weekParam ? parseInt(weekParam, 10) : null;

  const { data: matchupsResponse, isLoading } = useMatchups(leagueId, weekNumber);
  const { data: myPicks = [] } = usePicks(leagueId, weekNumber);
  const submitPick = useSubmitPick();

  // Find this specific matchup from the cached list by contestId
  const matchup = matchupsResponse?.matchups.find((m) => m.contestId === id) ?? null;

  const pickType = matchupsResponse?.pickType ?? 'StraightUp';

  const existingPick = myPicks.find((p) => p.contestId === id) ?? null;

  const status = matchup?.status.toLowerCase();
  const isLocked =
    status === 'inprogress' ||
    status === 'ongoing' ||
    status === 'halftime' ||
    status === 'final' ||
    status === 'completed';

  const handlePick = async (choice: PickChoice, franchiseSeasonId: string) => {
    if (!matchup || !leagueId || !weekNumber) return;
    try {
      await submitPick.mutateAsync({
        pickemGroupId: leagueId,
        contestId: matchup.contestId,
        pickType,
        franchiseSeasonId,
        week: weekNumber,
      });
    } catch {
      Alert.alert('Error', 'Could not save your pick. Please try again.');
    }
  };

  if (isLoading || !matchup) {
    return <LoadingSpinner fullScreen message="Loading game…" />;
  }

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: theme.background }]}
      contentContainerStyle={styles.content}
      showsVerticalScrollIndicator={false}
    >
      {/* Scoreboard */}
      <MatchupCard matchup={matchup} pick={existingPick} />

      {/* Pick selector */}
      {leagueId && (
        <PickSelector
          homeTeamName={matchup.home}
          awayTeamName={matchup.away}
          homeFranchiseSeasonId={matchup.homeFranchiseSeasonId}
          awayFranchiseSeasonId={matchup.awayFranchiseSeasonId}
          existingPickFranchiseId={existingPick?.franchiseId ?? null}
          isLocked={isLocked}
          onPick={handlePick}
        />
      )}

      {/* Venue detail */}
      {matchup.venue && (
        <View style={[styles.infoCard, { backgroundColor: theme.card, borderColor: theme.border }]}>
          <Text style={[styles.infoTitle, { color: theme.textMuted }]}>Venue</Text>
          <Text style={[styles.infoValue, { color: theme.text }]}>
            {matchup.venue}
          </Text>
          {(matchup.venueCity || matchup.venueState) && (
            <Text style={[styles.infoMeta, { color: theme.textMuted }]}>
              {[matchup.venueCity, matchup.venueState].filter(Boolean).join(', ')}
            </Text>
          )}
        </View>
      )}
    </ScrollView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { padding: 14, gap: 14, paddingBottom: 40 },
  pickSection: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
    gap: 12,
  },
  pickTitle: { fontSize: 16, fontWeight: '700' },
  pickSubtitle: { fontSize: 13 },
  pickButtons: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  pickBtn: {
    flex: 1,
    borderWidth: 1.5,
    borderRadius: 12,
    padding: 14,
    alignItems: 'center',
    gap: 4,
  },
  pickBtnSelected: {
    borderColor: Colors.brand.navy,
    backgroundColor: '#EEF2FF',
  },
  pickBtnLabel: {
    fontSize: 10,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  pickBtnTeam: {
    fontSize: 14,
    fontWeight: '700',
    textAlign: 'center',
  },
  vsBox: {
    width: 30,
    height: 30,
    borderRadius: 15,
    alignItems: 'center',
    justifyContent: 'center',
  },
  vsText: { fontSize: 13, fontWeight: '700' },
  infoCard: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
    gap: 4,
  },
  infoTitle: {
    fontSize: 11,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: 4,
  },
  infoValue: { fontSize: 16, fontWeight: '600' },
  infoMeta: { fontSize: 13 },
});
