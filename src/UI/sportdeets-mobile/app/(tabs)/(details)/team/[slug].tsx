import React, { useState } from 'react';
import {
  View,
  Text,
  Image,
  ScrollView,
  TouchableOpacity,
  StyleSheet,
} from 'react-native';
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { useColorScheme } from 'react-native';
import { getTheme } from '@/constants/Colors';
import { LoadingSpinner } from '@/src/components/ui/LoadingSpinner';
import { useTeamCard } from '@/src/hooks/useTeamCard';
import type { TeamCardScheduleGame } from '@/src/types/models';

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatKickoff(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

// ─── Season Selector ─────────────────────────────────────────────────────────

function SeasonSelector({
  years,
  selected,
  onSelect,
}: {
  years: number[];
  selected: number;
  onSelect: (year: number) => void;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={styles.seasonRow}
    >
      {years.map((yr) => {
        const active = yr === selected;
        return (
          <TouchableOpacity
            key={yr}
            onPress={() => onSelect(yr)}
            style={[
              styles.seasonPill,
              { borderColor: theme.border },
              active && { backgroundColor: theme.tint, borderColor: theme.tint },
            ]}
            activeOpacity={0.7}
          >
            <Text style={[styles.seasonPillText, { color: active ? '#fff' : theme.textMuted }]}>
              {yr}
            </Text>
          </TouchableOpacity>
        );
      })}
    </ScrollView>
  );
}

// ─── Schedule Row ─────────────────────────────────────────────────────────────

function ScheduleRow({ game, teamName }: { game: TeamCardScheduleGame; teamName: string }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();

  const isFinalized = !!game.finalizedUtc;
  const resultLabel = isFinalized
    ? `${game.wasWinner ? 'W' : 'L'} ${game.awayScore ?? 0}–${game.homeScore ?? 0}`
    : 'TBD';
  const resultColor = !isFinalized
    ? theme.textMuted
    : game.wasWinner
    ? theme.success
    : theme.error;

  return (
    <View style={[styles.gameRow, { borderBottomColor: theme.separator }]}>
      <Text style={[styles.gameDate, { color: theme.textMuted }]} numberOfLines={2}>
        {formatKickoff(game.date)}
      </Text>

      <View style={styles.gameMiddle}>
        {game.opponentSlug ? (
          <TouchableOpacity
            onPress={() => router.push({ pathname: '/team/[slug]', params: { slug: game.opponentSlug!, backTitle: teamName } } as never)}
            activeOpacity={0.7}
          >
            <Text style={[styles.gameOpponent, { color: theme.tint }]} numberOfLines={1}>
              {game.opponent}
            </Text>
          </TouchableOpacity>
        ) : (
          <Text style={[styles.gameOpponent, { color: theme.text }]} numberOfLines={1}>
            {game.opponent}
          </Text>
        )}
        {game.location ? (
          <Text style={[styles.gameLocation, { color: theme.textMuted }]} numberOfLines={1}>
            {game.location}
          </Text>
        ) : null}
      </View>

      {isFinalized && game.contestId ? (
        <TouchableOpacity
          onPress={() => router.push({ pathname: '/game/[id]', params: { id: game.contestId!, backTitle: teamName } } as never)}
          activeOpacity={0.7}
          style={styles.gameResult}
        >
          <Text style={[styles.gameResultText, { color: resultColor }]}>{resultLabel}</Text>
        </TouchableOpacity>
      ) : (
        <View style={styles.gameResult}>
          <Text style={[styles.gameResultText, { color: resultColor }]}>{resultLabel}</Text>
        </View>
      )}
    </View>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

// TODO: revert to new Date().getFullYear() once 2026 season data is sourced
const CURRENT_YEAR = 2025;

export default function TeamCard() {
  const { slug, season: seasonParam } = useLocalSearchParams<{
    slug: string;
    season?: string;
  }>();
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const initialSeason = seasonParam ? parseInt(seasonParam, 10) : CURRENT_YEAR;
  const [selectedSeason, setSelectedSeason] = useState(initialSeason);

  const { data: team, isLoading, error } = useTeamCard(slug, selectedSeason);

  if (isLoading) {
    return (
      <>
        <Stack.Screen options={{ title: 'Loading…' }} />
        <LoadingSpinner fullScreen message="Loading team…" />
      </>
    );
  }

  if (error || !team) {
    return (
      <>
        <Stack.Screen options={{ title: 'Team' }} />
        <View style={[styles.errorContainer, { backgroundColor: theme.background }]}>
          <Text style={[styles.errorText, { color: theme.error }]}>Team not found.</Text>
        </View>
      </>
    );
  }

  const availableYears = team.seasonYears ?? [selectedSeason];

  return (
    <>
      <Stack.Screen options={{ title: team.name }} />
      <ScrollView
        style={[styles.container, { backgroundColor: theme.background }]}
        contentContainerStyle={styles.content}
        showsVerticalScrollIndicator={false}
      >
        <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
          <View style={styles.headerRow}>
            {team.logoUrl ? (
              <Image source={{ uri: team.logoUrl }} style={styles.teamLogo} resizeMode="contain" />
            ) : (
              <View style={[styles.teamLogoPlaceholder, { backgroundColor: theme.separator }]} />
            )}
            <View style={styles.headerInfo}>
              <Text style={[styles.teamName, { color: theme.text }]}>{team.name}</Text>
              {team.conferenceName ? (
                <Text style={[styles.metaText, { color: theme.textMuted }]}>
                  {team.conferenceName}
                  {team.conferenceShortName ? ` (${team.conferenceShortName})` : ''}
                </Text>
              ) : null}
              {team.overallRecord ? (
                <Text style={[styles.recordText, { color: theme.text }]}>
                  {team.overallRecord}
                  {team.conferenceRecord ? `  (${team.conferenceRecord})` : ''}
                </Text>
              ) : null}
              {team.stadiumName ? (
                <Text style={[styles.metaText, { color: theme.textMuted }]} numberOfLines={1}>
                  {team.stadiumName}
                  {team.stadiumCapacity && team.stadiumCapacity > 0
                    ? ` – ${team.stadiumCapacity.toLocaleString()}`
                    : ''}
                </Text>
              ) : null}
            </View>
          </View>
        </View>

        {availableYears.length > 1 ? (
          <SeasonSelector
            years={availableYears}
            selected={selectedSeason}
            onSelect={setSelectedSeason}
          />
        ) : null}

        <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
          <Text style={[styles.sectionTitle, { color: theme.text }]}>
            Schedule ({selectedSeason})
          </Text>
          {team.schedule?.length ? (
            team.schedule.map((game, idx) => (
              <ScheduleRow key={game.contestId ?? idx} game={game} teamName={team.name} />
            ))
          ) : (
            <Text style={[styles.emptyText, { color: theme.textMuted }]}>No games scheduled.</Text>
          )}
        </View>
      </ScrollView>
    </>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { padding: 14, gap: 14, paddingBottom: 40 },

  card: { borderRadius: 14, borderWidth: StyleSheet.hairlineWidth, overflow: 'hidden' },

  headerRow: { flexDirection: 'row', alignItems: 'flex-start', padding: 16, gap: 14 },
  teamLogo: { width: 64, height: 64 },
  teamLogoPlaceholder: { width: 64, height: 64, borderRadius: 32 },
  headerInfo: { flex: 1, gap: 3 },
  teamName: { fontSize: 18, fontWeight: '700' },
  metaText: { fontSize: 13 },
  recordText: { fontSize: 15, fontWeight: '600', marginTop: 2 },

  seasonRow: { paddingHorizontal: 14, gap: 8, paddingVertical: 2 },
  seasonPill: { paddingHorizontal: 14, paddingVertical: 6, borderRadius: 20, borderWidth: 1.5 },
  seasonPillText: { fontSize: 13, fontWeight: '600' },

  sectionTitle: {
    fontSize: 13,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    paddingHorizontal: 16,
    paddingTop: 14,
    paddingBottom: 10,
  },
  emptyText: { fontSize: 13, paddingHorizontal: 16, paddingBottom: 16 },

  gameRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderBottomWidth: StyleSheet.hairlineWidth,
    gap: 10,
  },
  gameDate: { fontSize: 11, width: 72 },
  gameMiddle: { flex: 1, gap: 2 },
  gameOpponent: { fontSize: 13, fontWeight: '600' },
  gameLocation: { fontSize: 11 },
  gameResult: { width: 64, alignItems: 'flex-end' },
  gameResultText: { fontSize: 13, fontWeight: '700' },

  errorContainer: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 32 },
  errorText: { fontSize: 16, fontWeight: '600' },
});
