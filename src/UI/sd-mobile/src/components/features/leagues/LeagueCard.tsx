import React from 'react';
import { View, TouchableOpacity, StyleSheet, ActivityIndicator } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useQuery } from '@tanstack/react-query';

import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { leaguesApi, type LeagueSummary } from '@/src/services/api/leaguesApi';

// Mirrors YourLeaguesCard's SPORT_ICON map.
const SPORT_ICON: Record<LeagueSummary['sport'], string> = {
  FootballNcaa: '🏈',
  FootballNfl: '🏈',
  BaseballMlb: '⚾',
};

const PICK_TYPE_LABEL: Record<string, string> = {
  StraightUp: 'Straight Up',
  AgainstTheSpread: 'Against The Spread',
  OverUnder: 'Over / Under',
};

const TIEBREAKER_LABEL: Record<string, string> = {
  TotalPoints: 'Total Points',
  HomeAndAwayScores: 'Home & Away Scores',
  EarliestSubmission: 'Earliest Pick',
};

export const leagueDetailKeys = {
  byId: (id: string) => ['leagues', 'detail', id] as const,
};

/**
 * Formats the league window the same way sd-ui's LeagueDetail does: both bounds
 * null means full season, otherwise an open-ended side reads as "—".
 */
function formatWindow(startsOn: string | null, endsOn: string | null): string {
  if (!startsOn && !endsOn) return 'Full season';
  const fmt = (iso: string | null) =>
    iso ? new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }) : '—';
  return `${fmt(startsOn)} – ${fmt(endsOn)}`;
}

function Row({ label, value }: { label: string; value: string }) {
  const theme = getTheme(useColorScheme());
  return (
    <View style={styles.detailRow}>
      <Text style={[styles.detailLabel, { color: theme.textMuted }]}>{label}</Text>
      <Text style={[styles.detailValue, { color: theme.text }]}>{value}</Text>
    </View>
  );
}

interface Props {
  league: LeagueSummary;
  expanded: boolean;
  onToggleExpanded: () => void;
  onOpenPicks: () => void;
  onDuplicate: () => void;
}

/**
 * One league on My Leagues: summary header always visible, with a collapsible
 * overview standing in for web's dedicated /app/league/{id} page. The detail
 * fetch is lazy (enabled only while expanded), so opening the screen costs one
 * request rather than one per league.
 *
 * Past-season leagues render muted and drop Duplicate — the BE rejects cloning
 * a deactivated league regardless, so this is presentation only.
 */
export function LeagueCard({
  league,
  expanded,
  onToggleExpanded,
  onOpenPicks,
  onDuplicate,
}: Props) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const isPast = !!league.deactivatedUtc;

  const {
    data: detail,
    isLoading: detailLoading,
    isError: detailError,
    refetch: refetchDetail,
  } = useQuery({
    queryKey: leagueDetailKeys.byId(league.id),
    queryFn: () => leaguesApi.getLeagueById(league.id).then((r) => r.data),
    enabled: expanded,
  });

  return (
    <View
      style={[
        styles.card,
        { backgroundColor: theme.card, borderColor: theme.border },
        isPast && styles.cardPast,
      ]}
    >
      <View style={styles.cardHeader}>
        <Text
          style={styles.cardIcon}
          accessibilityElementsHidden
          importantForAccessibility="no-hide-descendants"
        >
          {SPORT_ICON[league.sport]}
        </Text>
        <Text style={[styles.cardName, { color: theme.text }]} numberOfLines={1}>
          {league.name}
        </Text>
        {isPast && (
          <View style={[styles.pastBadge, { borderColor: theme.border }]}>
            <Text style={[styles.pastBadgeText, { color: theme.textMuted }]}>Past</Text>
          </View>
        )}
      </View>

      <Text style={[styles.cardMeta, { color: theme.textMuted }]}>
        {PICK_TYPE_LABEL[league.leagueType] ?? league.leagueType}
        {league.useConfidencePoints ? ' · Confidence' : ''}
        {' · '}
        {league.memberCount} member{league.memberCount === 1 ? '' : 's'}
      </Text>

      <TouchableOpacity
        style={styles.disclosure}
        onPress={onToggleExpanded}
        accessibilityRole="button"
        accessibilityLabel={`${expanded ? 'Hide' : 'Show'} details for ${league.name}`}
        accessibilityState={{ expanded }}
      >
        <Text style={[styles.disclosureText, { color: theme.tint }]}>
          {expanded ? 'Hide details' : 'Details'}
        </Text>
        <Ionicons
          name={expanded ? 'chevron-up' : 'chevron-down'}
          size={14}
          color={theme.tint}
        />
      </TouchableOpacity>

      {expanded && (
        <View style={[styles.details, { borderTopColor: theme.border }]}>
          {detailLoading ? (
            <ActivityIndicator size="small" color={theme.tint} style={styles.detailLoading} />
          ) : detailError || !detail ? (
            // Split from the loading branch deliberately: a failed fetch leaves
            // isLoading false with detail undefined, so folding the two together
            // spins forever with no way out.
            <View style={styles.detailError}>
              <Text style={[styles.detailErrorText, { color: theme.textMuted }]}>
                Couldn&rsquo;t load details.
              </Text>
              <TouchableOpacity
                onPress={() => refetchDetail()}
                hitSlop={8}
                accessibilityRole="button"
                accessibilityLabel={`Retry loading details for ${league.name}`}
              >
                <Text style={[styles.detailRetry, { color: theme.tint }]}>Try again</Text>
              </TouchableOpacity>
            </View>
          ) : (
            <>
              {detail.description ? (
                <Text style={[styles.description, { color: theme.textMuted }]}>
                  {detail.description}
                </Text>
              ) : null}

              <Row
                label="Tiebreaker"
                value={TIEBREAKER_LABEL[detail.tiebreakerType] ?? detail.tiebreakerType}
              />
              <Row label="Window" value={formatWindow(detail.startsOn, detail.endsOn)} />
              <Row label="Visibility" value={detail.isPublic ? 'Public' : 'Private'} />
              {detail.rankingFilter ? (
                <Row label="Ranking filter" value={detail.rankingFilter.replace(/_/g, ' ')} />
              ) : null}
              <Row
                label={league.sport === 'FootballNcaa' ? 'Conferences' : 'Divisions'}
                value={
                  detail.conferenceSlugs.length > 0 ? detail.conferenceSlugs.join(', ') : 'All'
                }
              />

              <Text style={[styles.membersHeading, { color: theme.text }]}>
                Members ({detail.members.length})
              </Text>
              {detail.members.map((m) => (
                <View key={m.userId} style={styles.memberRow}>
                  <Text style={[styles.memberName, { color: theme.text }]} numberOfLines={1}>
                    {m.username}
                  </Text>
                  <Text style={[styles.memberRole, { color: theme.textMuted }]}>{m.role}</Text>
                </View>
              ))}
            </>
          )}
        </View>
      )}

      <View style={styles.cardActions}>
        <TouchableOpacity
          style={[styles.action, { backgroundColor: theme.tint }]}
          onPress={onOpenPicks}
          accessibilityRole="button"
          accessibilityLabel={`Open picks for ${league.name}`}
        >
          <Text style={[styles.actionText, { color: theme.textOnAccent }]}>Picks</Text>
        </TouchableOpacity>
        {!isPast && (
          <TouchableOpacity
            style={[styles.action, styles.actionSecondary, { borderColor: theme.border }]}
            onPress={onDuplicate}
            accessibilityRole="button"
            accessibilityLabel={`Duplicate ${league.name}`}
          >
            <Text style={[styles.actionText, { color: theme.text }]}>Duplicate</Text>
          </TouchableOpacity>
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
    gap: 8,
  },
  cardPast: { opacity: 0.75 },
  cardHeader: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  cardIcon: { fontSize: 18 },
  cardName: { fontSize: 17, fontWeight: '700', flexShrink: 1 },
  pastBadge: {
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 999,
    borderWidth: 1,
  },
  pastBadgeText: { fontSize: 10, fontWeight: '700' },
  cardMeta: { fontSize: 13 },
  disclosure: { flexDirection: 'row', alignItems: 'center', gap: 4, paddingVertical: 2 },
  disclosureText: { fontSize: 13, fontWeight: '700' },
  details: {
    borderTopWidth: StyleSheet.hairlineWidth,
    paddingTop: 10,
    gap: 6,
  },
  detailLoading: { paddingVertical: 12 },
  detailError: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
    paddingVertical: 8,
  },
  detailErrorText: { fontSize: 13, flexShrink: 1 },
  detailRetry: { fontSize: 13, fontWeight: '700' },
  description: { fontSize: 13, lineHeight: 18, marginBottom: 4 },
  detailRow: { flexDirection: 'row', justifyContent: 'space-between', gap: 12 },
  detailLabel: { fontSize: 13 },
  detailValue: { fontSize: 13, fontWeight: '600', flexShrink: 1, textAlign: 'right' },
  membersHeading: { fontSize: 13, fontWeight: '700', marginTop: 8 },
  memberRow: { flexDirection: 'row', justifyContent: 'space-between', gap: 12 },
  memberName: { fontSize: 13, flexShrink: 1 },
  memberRole: { fontSize: 12, textTransform: 'lowercase' },
  cardActions: { flexDirection: 'row', gap: 10, paddingTop: 6 },
  action: {
    flex: 1,
    paddingVertical: 10,
    borderRadius: 10,
    alignItems: 'center',
  },
  actionSecondary: { borderWidth: 1 },
  actionText: { fontSize: 14, fontWeight: '700' },
});
