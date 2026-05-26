import React from 'react';
import { View, TouchableOpacity, StyleSheet, ActivityIndicator } from 'react-native';
import { useRouter } from 'expo-router';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { resolveSportLeague } from '@/src/utils/sportLinks';
import { useUserTimeZone } from '@/src/hooks/useUserTimeZone';
import { formatToMonthDay } from '@/src/utils/timeUtils';
import type { TeamCardScheduleGame } from '@/src/types/models';

interface MiniScheduleProps {
  schedule: TeamCardScheduleGame[] | null | undefined;
  seasonYear: number;
  leagueSport?: string | null;
  loading?: boolean;
  error?: string | null;
  teamName: string;
}

function formatGameResult(game: TeamCardScheduleGame): string {
  if (!game.finalizedUtc) return 'TBD';
  const score = `${game.awayScore ?? 0}-${game.homeScore ?? 0}`;
  // wasWinner is nullable — a finalized game with unknown outcome (canceled,
  // tied, backend gap) shouldn't silently render as a loss.
  const resultText = game.wasWinner === true ? 'W' : game.wasWinner === false ? 'L' : '—';
  return `${resultText} | ${score}`;
}

export function MiniSchedule({ schedule, seasonYear, leagueSport, loading, error, teamName }: MiniScheduleProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const userTz = useUserTimeZone();
  const sportLeague = resolveSportLeague(leagueSport);

  if (loading) {
    return (
      <View style={[styles.container, { borderTopColor: theme.separator }]}>
        <ActivityIndicator size="small" color={theme.tint} />
      </View>
    );
  }

  if (error) {
    return (
      <View style={[styles.container, { borderTopColor: theme.separator }]}>
        <Text style={[styles.emptyText, { color: theme.error }]}>{error}</Text>
      </View>
    );
  }

  if (!Array.isArray(schedule) || schedule.length === 0) {
    return (
      <View style={[styles.container, { borderTopColor: theme.separator }]}>
        <Text style={[styles.emptyText, { color: theme.textMuted }]}>No recent games</Text>
      </View>
    );
  }

  // Endpoint already returns completed-only, newest-first, exclusive of `week`.
  // Football shows the entire (short) schedule; other sports cap at 10.
  const limit = sportLeague?.sport === 'football' ? schedule.length : 10;
  const games = schedule.slice(0, limit);

  return (
    <View style={[styles.container, { borderTopColor: theme.separator }]}>
      {games.map((game, idx) => {
        const opponentLabel = game.opponent ?? 'Opponent';
        const resultColor = !game.finalizedUtc
          ? theme.textMuted
          : game.wasWinner === true
          ? theme.success
          : game.wasWinner === false
          ? theme.error
          : theme.textMuted;
        const isLast = idx === games.length - 1;

        return (
          <View
            key={game.contestId ?? `${game.date}-${idx}`}
            style={[
              styles.row,
              !isLast && { borderBottomColor: theme.separator, borderBottomWidth: StyleSheet.hairlineWidth },
            ]}
          >
            <Text style={[styles.dateCell, { color: theme.textMuted }]} numberOfLines={1}>
              {formatToMonthDay(game.date, userTz)}
            </Text>

            <View style={styles.opponentCell}>
              {game.opponentSlug && sportLeague ? (
                <TouchableOpacity
                  onPress={() =>
                    router.push(
                      {
                        pathname: '/sport/[sport]/[league]/team/[slug]',
                        params: {
                          sport: sportLeague.sport,
                          league: sportLeague.league,
                          slug: game.opponentSlug!,
                          season: String(seasonYear),
                          backTitle: teamName,
                        },
                      } as never,
                    )
                  }
                  activeOpacity={0.6}
                >
                  <Text style={[styles.opponentText, { color: theme.tint }]} numberOfLines={1}>
                    {opponentLabel}
                  </Text>
                </TouchableOpacity>
              ) : (
                <Text style={[styles.opponentText, { color: theme.text }]} numberOfLines={1}>
                  {opponentLabel}
                </Text>
              )}
              {game.location ? (
                <Text style={[styles.locationText, { color: theme.textMuted }]} numberOfLines={1}>
                  {game.location}
                </Text>
              ) : null}
            </View>

            {game.finalizedUtc && game.contestId && sportLeague ? (
              <TouchableOpacity
                onPress={() =>
                  router.push(
                    {
                      pathname: '/sport/[sport]/[league]/game/[id]',
                      params: {
                        sport: sportLeague.sport,
                        league: sportLeague.league,
                        id: game.contestId!,
                        backTitle: teamName,
                      },
                    } as never,
                  )
                }
                activeOpacity={0.6}
                style={styles.resultCell}
              >
                <Text style={[styles.resultText, { color: resultColor }]}>{formatGameResult(game)}</Text>
              </TouchableOpacity>
            ) : (
              <View style={styles.resultCell}>
                <Text style={[styles.resultText, { color: resultColor }]}>{formatGameResult(game)}</Text>
              </View>
            )}
          </View>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: 14,
    paddingVertical: 4,
    borderTopWidth: StyleSheet.hairlineWidth,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 6,
    gap: 10,
  },
  dateCell: {
    width: 48,
    fontSize: 12,
  },
  opponentCell: {
    flex: 1,
  },
  opponentText: {
    fontSize: 13,
    fontWeight: '500',
  },
  locationText: {
    fontSize: 11,
    marginTop: 1,
  },
  resultCell: {
    minWidth: 70,
    alignItems: 'flex-end',
  },
  resultText: {
    fontSize: 13,
    fontWeight: '600',
  },
  emptyText: {
    fontSize: 13,
    textAlign: 'center',
    paddingVertical: 6,
  },
});
