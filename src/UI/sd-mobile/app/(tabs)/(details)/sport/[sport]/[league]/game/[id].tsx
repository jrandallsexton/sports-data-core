import React from 'react';
import {
  View,
  Text,
  Image,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
} from 'react-native';
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Colors, getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { useMatchups } from '@/src/hooks/useMatchups';
import { usePicks, useSubmitPick, useContestOverview } from '@/src/hooks/useContest';
import type {
  PickChoice,
  ContestOverviewDto,
  ContestOverviewLeaderCategory,
} from '@/src/types/models';
import {
  formatToUserTime,
  getStartLabel,
  getZoneAbbreviation,
} from '@/src/utils/timeUtils';
import { useUserTimeZone } from '@/src/hooks/useUserTimeZone';

// ─── BoxScore ─────────────────────────────────────────────────────────────────

function TeamScoreRow({
  team,
  total,
  isWinner,
  theme,
  gameTitle,
  sport,
  league,
}: {
  team: ContestOverviewDto['header']['homeTeam'];
  total: number;
  isWinner: boolean;
  theme: ReturnType<typeof getTheme>;
  gameTitle: string;
  sport: string;
  league: string;
}) {
  const router = useRouter();
  return (
    <View style={styles.teamScoreRow}>
      {team.logoUrl ? (
        <Image source={{ uri: team.logoUrl }} style={styles.logo} resizeMode="contain" />
      ) : (
        <View style={[styles.logoPlaceholder, { backgroundColor: theme.separator }]} />
      )}
      {team.slug ? (
        <TouchableOpacity
          style={styles.teamScoreNameWrapper}
          onPress={() =>
            router.push(
              {
                pathname: '/sport/[sport]/[league]/team/[slug]',
                params: {
                  sport,
                  league,
                  slug: team.slug!,
                  backTitle: gameTitle,
                },
              } as never,
            )
          }
          activeOpacity={0.7}
        >
          <Text
            style={[
              styles.teamScoreName,
              { color: theme.tint, fontWeight: isWinner ? '700' : '400' },
            ]}
            numberOfLines={1}
          >
            {team.displayName}
          </Text>
        </TouchableOpacity>
      ) : (
        <Text
          style={[
            styles.teamScoreName,
            { color: theme.text, fontWeight: isWinner ? '700' : '400' },
          ]}
          numberOfLines={1}
        >
          {team.displayName}
        </Text>
      )}
      <Text
        style={[
          styles.teamScoreTotal,
          { color: isWinner ? theme.text : theme.textMuted, fontWeight: isWinner ? '800' : '600' },
        ]}
      >
        {total}
      </Text>
    </View>
  );
}

function BoxScoreCard({
  homeTeam,
  awayTeam,
  quarterScores,
  gameTitle,
  sport,
  league,
}: ContestOverviewDto['header'] & { gameTitle: string; sport: string; league: string }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const awayTotal = quarterScores.reduce((s, q) => s + q.awayScore, 0);
  const homeTotal = quarterScores.reduce((s, q) => s + q.homeScore, 0);

  return (
    <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <TeamScoreRow team={awayTeam} total={awayTotal} isWinner={awayTotal > homeTotal} theme={theme} gameTitle={gameTitle} sport={sport} league={league} />
      <View style={[styles.rule, { backgroundColor: theme.separator }]} />
      <TeamScoreRow team={homeTeam} total={homeTotal} isWinner={homeTotal >= awayTotal} theme={theme} gameTitle={gameTitle} sport={sport} league={league} />

      {/* Quarter grid */}
      <View style={[styles.qsGrid, { borderTopColor: theme.border }]}>
        <View style={styles.qsRow}>
          <Text style={[styles.qsTeamCell, { color: theme.textMuted }]} />
          {quarterScores.map((q) => (
            <Text key={q.quarter} style={[styles.qsCell, { color: theme.textMuted }]}>
              {q.quarter}
            </Text>
          ))}
          <Text style={[styles.qsCellBold, { color: theme.textMuted }]}>T</Text>
        </View>
        <View style={styles.qsRow}>
          <Text style={[styles.qsTeamCell, { color: theme.text }]} numberOfLines={1}>
            {(awayTeam.displayName.split(' ').pop() ?? awayTeam.displayName).toUpperCase()}
          </Text>
          {quarterScores.map((q) => (
            <Text key={q.quarter} style={[styles.qsCell, { color: theme.text }]}>
              {q.awayScore}
            </Text>
          ))}
          <Text style={[styles.qsCellBold, { color: awayTotal > homeTotal ? theme.success : theme.text }]}>
            {awayTotal}
          </Text>
        </View>
        <View style={styles.qsRow}>
          <Text style={[styles.qsTeamCell, { color: theme.text }]} numberOfLines={1}>
            {(homeTeam.displayName.split(' ').pop() ?? homeTeam.displayName).toUpperCase()}
          </Text>
          {quarterScores.map((q) => (
            <Text key={q.quarter} style={[styles.qsCell, { color: theme.text }]}>
              {q.homeScore}
            </Text>
          ))}
          <Text style={[styles.qsCellBold, { color: homeTotal >= awayTotal ? theme.success : theme.text }]}>
            {homeTotal}
          </Text>
        </View>
      </View>
    </View>
  );
}

// ─── Leaders ─────────────────────────────────────────────────────────────────

function LeadersCard({
  categories,
  awayName,
  homeName,
}: {
  categories: ContestOverviewLeaderCategory[];
  awayName: string;
  homeName: string;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  if (!categories.length) return null;

  return (
    <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <Text style={[styles.sectionTitle, { color: theme.text }]}>Leaders</Text>
      {categories.map((cat, idx) => (
        <View key={cat.categoryId ?? idx}>
          {idx > 0 && <View style={[styles.rule, { backgroundColor: theme.border }]} />}
          <View style={styles.leaderCategoryRow}>
            {/* Away side */}
            <View style={styles.leaderSide}>
              {cat.away.leaders.map((l, i) => (
                <View key={i} style={styles.leaderPlayer}>
                  {l.playerHeadshotUrl ? (
                    <Image source={{ uri: l.playerHeadshotUrl }} style={styles.headshot} resizeMode="cover" />
                  ) : null}
                  <View style={{ flex: 1 }}>
                    <Text style={[styles.leaderName, { color: theme.text }]} numberOfLines={1}>
                      {l.playerName}
                    </Text>
                    {l.statLine ? (
                      <Text style={[styles.leaderStat, { color: theme.textMuted }]} numberOfLines={2}>
                        {l.statLine}
                      </Text>
                    ) : null}
                  </View>
                </View>
              ))}
            </View>

            {/* Category label */}
            <View style={styles.leaderCenterLabel}>
              <Text style={[styles.leaderCategory, { color: theme.textMuted }]} numberOfLines={2}>
                {cat.categoryName}
              </Text>
            </View>

            {/* Home side */}
            <View style={[styles.leaderSide, { alignItems: 'flex-end' }]}>
              {cat.home.leaders.map((l, i) => (
                <View key={i} style={[styles.leaderPlayer, { flexDirection: 'row-reverse' }]}>
                  {l.playerHeadshotUrl ? (
                    <Image source={{ uri: l.playerHeadshotUrl }} style={styles.headshot} resizeMode="cover" />
                  ) : null}
                  <View style={{ flex: 1, alignItems: 'flex-end' }}>
                    <Text style={[styles.leaderName, { color: theme.text }]} numberOfLines={1}>
                      {l.playerName}
                    </Text>
                    {l.statLine ? (
                      <Text style={[styles.leaderStat, { color: theme.textMuted }]} numberOfLines={2}>
                        {l.statLine}
                      </Text>
                    ) : null}
                  </View>
                </View>
              ))}
            </View>
          </View>
          <View style={styles.leaderTeamLabels}>
            <Text style={[styles.leaderTeamLabel, { color: theme.textMuted }]}>{awayName}</Text>
            <Text style={[styles.leaderTeamLabel, { color: theme.textMuted }]}>{homeName}</Text>
          </View>
        </View>
      ))}
    </View>
  );
}

// ─── Game Info ────────────────────────────────────────────────────────────────

function GameInfoCard({
  info,
  sport,
}: {
  info: ContestOverviewDto['info'];
  sport: string | null | undefined;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const userTz = useUserTimeZone();

  if (!info) return null;

  const rows: { label: string; value: string }[] = [];
  if (info.venue) rows.push({ label: 'Venue', value: info.venue });
  if (info.venueCity || info.venueState)
    rows.push({ label: 'Location', value: [info.venueCity, info.venueState].filter(Boolean).join(', ') });
  if (info.attendance)
    rows.push({ label: 'Attendance', value: Number(info.attendance).toLocaleString('en-US') });
  if (info.broadcast) rows.push({ label: 'Broadcast', value: info.broadcast });
  if (info.startDateUtc) {
    const startLabel = getStartLabel(sport);
    const tzAbbr = getZoneAbbreviation(userTz);
    rows.push({
      label: `${startLabel} (${tzAbbr})`,
      value: formatToUserTime(info.startDateUtc, userTz),
    });
  }

  if (!rows.length) return null;

  return (
    <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <Text style={[styles.sectionTitle, { color: theme.text }]}>Game Info</Text>
      {rows.map(({ label, value }, i) => (
        <View
          key={label}
          style={[
            styles.infoRow,
            i > 0 && { borderTopColor: theme.separator, borderTopWidth: StyleSheet.hairlineWidth },
          ]}
        >
          <Text style={[styles.infoLabel, { color: theme.textMuted }]}>{label}</Text>
          <Text style={[styles.infoValue, { color: theme.text }]}>{value}</Text>
        </View>
      ))}
    </View>
  );
}

// ─── Pick Selector ────────────────────────────────────────────────────────────

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
  submitPending: boolean;
  onPick: (choice: PickChoice, franchiseSeasonId: string) => void;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const pickedHome = existingPickFranchiseId === homeFranchiseSeasonId;
  const pickedAway = existingPickFranchiseId === awayFranchiseSeasonId;
  const hasPick = pickedHome || pickedAway;
  const locked = isLocked || submitPending;

  return (
    <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <Text style={[styles.sectionTitle, { color: theme.text }]}>
        {isLocked ? '🔒 Picks Locked' : hasPick ? '✓ Pick Submitted' : 'Make Your Pick'}
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
            style={[styles.pickBtnTeam, { color: pickedAway ? Colors.brand.navy : theme.text }]}
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
            style={[styles.pickBtnTeam, { color: pickedHome ? Colors.brand.navy : theme.text }]}
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
  const {
    sport,
    league,
    id,
    leagueId,
    week: weekParam,
  } = useLocalSearchParams<{
    sport: string;
    league: string;
    id: string;
    leagueId?: string;
    week?: string;
  }>();
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const weekNumber = weekParam ? parseInt(weekParam, 10) : null;

  const { data: overview, isLoading: overviewLoading, error: overviewError } = useContestOverview(id, sport, league);

  const { data: matchupsResponse } = useMatchups(leagueId, weekNumber);
  const { data: myPicks = [] } = usePicks(leagueId, weekNumber);
  const submitPick = useSubmitPick();

  const matchup = matchupsResponse?.matchups.find((m) => m.contestId === id) ?? null;
  const pickType = matchupsResponse?.pickType ?? 'StraightUp';
  const existingPick = myPicks.find((p) => p.contestId === id) ?? null;

  const matchupStatus = matchup?.status.toLowerCase();
  const kickoffMs = matchup ? new Date(matchup.startDateUtc).getTime() : NaN;
  const isLocked =
    matchupStatus === 'inprogress' ||
    matchupStatus === 'ongoing' ||
    matchupStatus === 'halftime' ||
    matchupStatus === 'final' ||
    matchupStatus === 'completed' ||
    (!isNaN(kickoffMs) && Date.now() >= kickoffMs - 5 * 60 * 1000);

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

  const screenTitle = overview
    ? `${overview.header.awayTeam.displayName} @ ${overview.header.homeTeam.displayName}`
    : matchup
    ? `${matchup.awayShort} @ ${matchup.homeShort}`
    : 'Game Detail';

  if (overviewLoading) {
    return (
      <>
        <Stack.Screen options={{ title: 'Loading…' }} />
        <LoadingSpinner fullScreen message="Loading game…" />
      </>
    );
  }

  return (
    <>
      <Stack.Screen options={{ title: screenTitle }} />
      <ScrollView
        style={[styles.container, { backgroundColor: theme.background }]}
        contentContainerStyle={styles.content}
        showsVerticalScrollIndicator={false}
      >
        {overviewError || !overview ? (
          <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border, padding: 16, gap: 4 }]}>
            <Text style={[styles.errorText, { color: theme.error }]}>Could not load game data.</Text>
            <Text style={[styles.errorSub, { color: theme.textMuted }]}>
              This game may not have overview data yet.
            </Text>
          </View>
        ) : (
          <>
            <BoxScoreCard
              homeTeam={overview.header.homeTeam}
              awayTeam={overview.header.awayTeam}
              quarterScores={overview.header.quarterScores}
              gameTitle={screenTitle}
              sport={sport}
              league={league}
            />
            {overview.leaders?.categories?.length ? (
              <LeadersCard
                categories={overview.leaders.categories}
                awayName={overview.header.awayTeam.displayName}
                homeName={overview.header.homeTeam.displayName}
              />
            ) : null}
            <GameInfoCard info={overview.info} sport={sport} />
          </>
        )}
        {leagueId && matchup ? (
          <PickSelector
            homeTeamName={matchup.home}
            awayTeamName={matchup.away}
            homeFranchiseSeasonId={matchup.homeFranchiseSeasonId}
            awayFranchiseSeasonId={matchup.awayFranchiseSeasonId}
            existingPickFranchiseId={existingPick?.franchiseId ?? null}
            isLocked={isLocked}
            submitPending={submitPick.isPending}
            onPick={handlePick}
          />
        ) : null}
      </ScrollView>
    </>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { padding: 14, gap: 14, paddingBottom: 40 },

  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    overflow: 'hidden',
  },
  rule: { height: StyleSheet.hairlineWidth },

  sectionTitle: {
    fontSize: 13,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    paddingHorizontal: 16,
    paddingTop: 14,
    paddingBottom: 10,
  },

  teamScoreRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 12,
    gap: 10,
  },
  logo: { width: 36, height: 36 },
  logoPlaceholder: { width: 36, height: 36, borderRadius: 18 },
  teamScoreNameWrapper: { flex: 1 },
  teamScoreName: { flex: 1, fontSize: 15 },
  teamScoreTotal: { fontSize: 24, minWidth: 36, textAlign: 'right' },

  qsGrid: {
    borderTopWidth: StyleSheet.hairlineWidth,
    paddingHorizontal: 16,
    paddingVertical: 10,
    gap: 4,
  },
  qsRow: { flexDirection: 'row', alignItems: 'center' },
  qsTeamCell: { width: 56, fontSize: 11, fontWeight: '600', textTransform: 'uppercase' },
  qsCell: { flex: 1, fontSize: 13, textAlign: 'center' },
  qsCellBold: { width: 36, fontSize: 13, fontWeight: '700', textAlign: 'right' },

  leaderCategoryRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    paddingHorizontal: 12,
    paddingVertical: 12,
    gap: 8,
  },
  leaderSide: { flex: 1, gap: 6 },
  leaderCenterLabel: { width: 76, alignItems: 'center', paddingTop: 4 },
  leaderCategory: {
    fontSize: 10,
    fontWeight: '700',
    textTransform: 'uppercase',
    textAlign: 'center',
    letterSpacing: 0.3,
  },
  leaderPlayer: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  headshot: { width: 30, height: 30, borderRadius: 15 },
  leaderName: { fontSize: 12, fontWeight: '600' },
  leaderStat: { fontSize: 11 },
  leaderTeamLabels: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: 12,
    paddingBottom: 8,
  },
  leaderTeamLabel: { fontSize: 10, fontWeight: '600', textTransform: 'uppercase' },

  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 10,
    gap: 12,
    alignItems: 'flex-start',
  },
  infoLabel: {
    fontSize: 12,
    fontWeight: '600',
    textTransform: 'uppercase',
    letterSpacing: 0.3,
    width: 116,
    paddingTop: 1,
  },
  infoValue: { fontSize: 13, flex: 1, textAlign: 'right' },

  pickSubtitle: { fontSize: 13, paddingHorizontal: 16, marginTop: -6, marginBottom: 4 },
  pickButtons: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 16,
    paddingBottom: 14,
  },
  pickBtn: {
    flex: 1,
    borderWidth: 1.5,
    borderRadius: 12,
    padding: 14,
    alignItems: 'center',
    gap: 4,
  },
  pickBtnSelected: { borderColor: Colors.brand.navy, backgroundColor: '#EEF2FF' },
  pickBtnLabel: {
    fontSize: 10,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  pickBtnTeam: { fontSize: 14, fontWeight: '700', textAlign: 'center' },
  vsBox: { width: 30, height: 30, borderRadius: 15, alignItems: 'center', justifyContent: 'center' },
  vsText: { fontSize: 13, fontWeight: '700' },

  errorContainer: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 32 },
  errorText: { fontSize: 16, fontWeight: '600', marginBottom: 8, textAlign: 'center' },
  errorSub: { fontSize: 13, textAlign: 'center' },
});
