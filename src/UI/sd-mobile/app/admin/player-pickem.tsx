import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  View,
  StyleSheet,
  ScrollView,
  FlatList,
  TouchableOpacity,
} from 'react-native';
import { Stack } from 'expo-router';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { useCurrentUser } from '@/src/hooks/useStandings';
import {
  getAthletesByPosition,
  type PickemAthlete,
} from '@/src/services/api/playerPickemApi';
import {
  SLOT_DEFS,
  slotById,
  eligiblePositions,
  assign,
  remove,
  isRostered,
  type Roster,
} from '@/src/utils/pickem/rosterLogic';
import {
  statPartsFor,
  seasonLine,
  sortAthletes,
  NAME_SORT,
  type SortDescriptor,
} from '@/src/utils/pickem/athleteStats';

// Selections survive app restarts — rudimentary stand-in for carry-over
// until PlayerLineup entities exist server-side. Shares nothing with the
// web draft; both are local explorations.
const ROSTER_KEY = 'playerPickemRosterDraft';

/**
 * A stored draft is untrusted input: JSON.parse happily returns null,
 * arrays, or strings (all valid JSON), and a raw setRoster of any of
 * those crashes the screen on the next roster[activeSlotId] read. Accept
 * only a plain object whose keys are real slot ids and whose values look
 * like athletes; anything else degrades to the slot-by-slot best effort
 * or an empty roster.
 */
function sanitizeRoster(raw: string): Roster {
  try {
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
      return {};
    }
    const next: Roster = {};
    for (const [slotId, val] of Object.entries(parsed)) {
      if (
        slotById(slotId) &&
        val !== null &&
        typeof val === 'object' &&
        !Array.isArray(val) &&
        typeof (val as { athleteId?: unknown }).athleteId === 'string'
      ) {
        next[slotId] = val as PickemAthlete;
      }
    }
    return next;
  } catch {
    return {};
  }
}

const OPP_DEF_LABEL: Record<string, string> = {
  QB: 'Pass Alw/G',
  RB: 'Rush Alw/G',
  WR: 'Pass Alw/G',
  TE: 'Pass Alw/G',
  K: 'Pts Alw/G',
  FLEX: 'Def/G',
};

/**
 * Player Pick'em roster builder — admin-only v1 exploration, mobile
 * parity for sd-ui's PlayerRosterBuilder. Same slot shape, same mock
 * Week 5 contract; the web's mirrored table columns become stacked
 * '26/'25 lines on a card, and column-header sorting becomes a chip row.
 */
export default function PlayerPickemScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  // Defense-in-depth admin gate — same convention as /admin/push-token:
  // the profile link is gated, but the route is reachable by direct URL.
  const { data: me, isLoading: meLoading } = useCurrentUser();
  const isAdmin = me?.isAdmin === true;

  const [roster, setRoster] = useState<Roster>({});
  const [rosterHydrated, setRosterHydrated] = useState(false);
  const [activeSlotId, setActiveSlotId] = useState('QB');
  const [athletes, setAthletes] = useState<PickemAthlete[]>([]);
  const [loading, setLoading] = useState(false);
  const [sort, setSort] = useState<SortDescriptor>(NAME_SORT);

  useEffect(() => {
    let cancelled = false;
    AsyncStorage.getItem(ROSTER_KEY)
      .then((raw) => {
        if (cancelled || !raw) return;
        setRoster(sanitizeRoster(raw));
      })
      .finally(() => {
        if (!cancelled) setRosterHydrated(true);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    // Don't clobber a stored draft with the initial empty roster before
    // hydration has read it.
    if (!rosterHydrated) return;
    AsyncStorage.setItem(ROSTER_KEY, JSON.stringify(roster)).catch(() => {});
  }, [roster, rosterHydrated]);

  const positions = useMemo(
    () => eligiblePositions(activeSlotId),
    [activeSlotId]
  );

  const parts = useMemo(
    () => statPartsFor(activeSlotId, positions),
    [activeSlotId, positions]
  );

  // Stat sets differ per slot — slot changes reset to the name sort.
  useEffect(() => {
    setSort(NAME_SORT);
  }, [activeSlotId]);

  useEffect(() => {
    if (positions.length === 0) return;
    let ignore = false;
    setLoading(true);

    Promise.all(positions.map((pos) => getAthletesByPosition(pos)))
      .then((responses) => {
        if (ignore) return;
        setAthletes(responses.flatMap((r) => r.athletes));
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [positions]);

  const sorted = useMemo(
    () => sortAthletes(athletes, sort, parts),
    [athletes, sort, parts]
  );

  const toggleSort = useCallback((key: string) => {
    setSort((prev) =>
      prev.key === key
        ? { key, dir: prev.dir === 'desc' ? 'asc' : 'desc' }
        : { key, dir: 'desc' }
    );
  }, []);

  const activeSlot = SLOT_DEFS.find((s) => s.id === activeSlotId);
  const occupant = roster[activeSlotId];
  const oppDefLabel =
    OPP_DEF_LABEL[activeSlot?.id === 'FLEX' ? 'FLEX' : positions[0]];

  if (meLoading) {
    return (
      <>
        <Stack.Screen options={{ title: "Player Pick'em", headerBackTitle: 'Back' }} />
        <View style={[styles.gate, { backgroundColor: theme.background }]}>
          <Text style={{ color: theme.textMuted }}>Loading…</Text>
        </View>
      </>
    );
  }

  if (!isAdmin) {
    return (
      <>
        <Stack.Screen options={{ title: "Player Pick'em", headerBackTitle: 'Back' }} />
        <View style={[styles.gate, { backgroundColor: theme.background }]}>
          <Text style={[styles.gateHeading, { color: theme.text }]}>Unauthorized</Text>
          <Text style={{ color: theme.textMuted, textAlign: 'center' }}>
            This screen is restricted to admin users.
          </Text>
        </View>
      </>
    );
  }

  const renderAthlete = ({ item: a }: { item: PickemAthlete }) => {
    const rostered = isRostered(roster, a.athleteId);
    const addLabel =
      occupant && occupant.athleteId !== a.athleteId
        ? `Replace ${occupant.firstName.charAt(0)}. ${occupant.lastName}`
        : 'Add';

    return (
      <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
        <View style={styles.cardHeader}>
          <View style={styles.cardIdentity}>
            <Text style={[styles.cardName, { color: theme.text }]}>
              {a.lastName}, {a.firstName}
              {activeSlot?.id === 'FLEX' ? (
                <Text style={[styles.cardPos, { color: theme.textMuted }]}>
                  {'  '}{a.position}
                </Text>
              ) : null}
            </Text>
            <Text style={[styles.cardTeam, { color: theme.textMuted }]}>
              {a.teamName}
            </Text>
          </View>
          <TouchableOpacity
            style={[
              styles.addBtn,
              { borderColor: rostered ? theme.border : theme.tint },
            ]}
            disabled={rostered}
            onPress={() => setRoster((prev) => assign(prev, activeSlotId, a))}
          >
            <Text
              style={[
                styles.addBtnText,
                { color: rostered ? theme.textMuted : theme.tint },
              ]}
            >
              {rostered ? 'Rostered' : addLabel}
            </Text>
          </TouchableOpacity>
        </View>

        <Text style={[styles.cardMatchup, { color: theme.textMuted }]}>
          {a.opponentName ? `vs ${a.opponentName}` : 'BYE'}
          {a.opponentDefPerGame != null
            // Per-row label: on FLEX the number's meaning depends on the
            // athlete's position (rush vs pass allowed), so the card says
            // which it is instead of the slot's generic label.
            ? ` · Opp ${a.opponentDefPerGame.toFixed(1)} ${OPP_DEF_LABEL[a.position]}`
            : ''}
        </Text>

        {/* '26 over '25 in the same stat order — the mobile translation of
            the web grid's mirrored columns. */}
        <View style={styles.seasonRow}>
          <Text style={[styles.seasonTag, { color: theme.text }]}>
            {a.currentSeason
              ? `'26 · ${a.currentSeason.gamesPlayed} G`
              : "'26 · —"}
          </Text>
          <Text style={[styles.seasonStats, { color: theme.text }]} numberOfLines={1}>
            {a.currentSeason
              ? seasonLine(parts, a.currentSeason, a)
              : 'No games yet'}
          </Text>
        </View>
        <View style={styles.seasonRow}>
          <Text style={[styles.seasonTag, { color: theme.textMuted }]}>
            {a.previousSeason
              ? `'25 · ${a.previousSeason.gamesPlayed} G`
              : "'25 · —"}
          </Text>
          <Text style={[styles.seasonStats, { color: theme.textMuted }]} numberOfLines={1}>
            {a.previousSeason
              ? seasonLine(parts, a.previousSeason, a)
              : 'No prior season'}
          </Text>
        </View>
      </View>
    );
  };

  return (
    <>
      <Stack.Screen options={{ title: "Player Pick'em", headerBackTitle: 'Back' }} />
      <View style={[styles.container, { backgroundColor: theme.background }]}>
        <Text style={[styles.sub, { color: theme.textMuted }]}>
          Week 5 · 2026 · NCAAFB (FBS) — admin preview, local-only, mock data
        </Text>

        {/* Wrapping grid, not a horizontal scroller — every slot visible at
            once so nothing about the lineup shape hides off-screen. */}
        <View style={styles.slotRow}>
          {SLOT_DEFS.map((slot) => {
            const filled = roster[slot.id];
            const isActive = slot.id === activeSlotId;
            return (
              <TouchableOpacity
                key={slot.id}
                disabled={slot.disabled}
                onPress={() => setActiveSlotId(slot.id)}
                style={[
                  styles.slot,
                  { borderColor: theme.border },
                  filled && { borderColor: theme.tint, borderStyle: 'solid' },
                  isActive && { borderColor: theme.tint, borderWidth: 2 },
                  slot.disabled && styles.slotDisabled,
                ]}
              >
                <Text
                  style={[
                    styles.slotLabel,
                    { color: filled ? theme.tint : theme.textMuted },
                  ]}
                >
                  {slot.label}
                </Text>
                <Text
                  style={[
                    styles.slotPlayer,
                    { color: filled ? theme.text : theme.textMuted },
                  ]}
                  numberOfLines={1}
                >
                  {filled
                    ? `${filled.firstName.charAt(0)}. ${filled.lastName}`
                    : slot.disabled
                      ? 'Soon'
                      : '—'}
                </Text>
                {filled ? (
                  <TouchableOpacity
                    style={[styles.slotRemove, { backgroundColor: theme.card, borderColor: theme.border }]}
                    accessibilityLabel={`Remove ${filled.firstName} ${filled.lastName}`}
                    onPress={() => setRoster((prev) => remove(prev, slot.id))}
                  >
                    <Text style={{ color: theme.textMuted, fontSize: 10, lineHeight: 12 }}>✕</Text>
                  </TouchableOpacity>
                ) : null}
              </TouchableOpacity>
            );
          })}
        </View>

        {/* Sort chips replace the web's clickable column headers. */}
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          style={styles.sortRow}
          contentContainerStyle={styles.sortRowContent}
        >
          {[
            { key: 'name', label: 'Name' },
            ...parts.map((p) => ({ key: p.key, label: p.label })),
            // No opponent-defense sort on FLEX: the value is rush yds
            // allowed/G for an RB but pass yds allowed/G for a WR/TE —
            // different units, so a cross-position ranking would lie.
            // (The per-card display stays; each card's label carries its
            // own meaning.)
            ...(activeSlotId === 'FLEX'
              ? []
              : [{ key: 'oppDef', label: `Opp ${oppDefLabel}` }]),
          ].map((chip) => {
            const active = sort.key === chip.key;
            const arrow =
              active && chip.key !== 'name'
                ? sort.dir === 'asc'
                  ? ' ▲'
                  : ' ▼'
                : '';
            return (
              <TouchableOpacity
                key={chip.key}
                onPress={() =>
                  chip.key === 'name' ? setSort(NAME_SORT) : toggleSort(chip.key)
                }
                style={[
                  styles.sortChip,
                  { borderColor: active ? theme.tint : theme.border },
                ]}
              >
                <Text
                  style={{
                    color: active ? theme.tint : theme.textMuted,
                    fontSize: 12,
                    fontWeight: '600',
                  }}
                >
                  {chip.label}
                  {arrow}
                </Text>
              </TouchableOpacity>
            );
          })}
        </ScrollView>

        {loading ? (
          <Text style={[styles.status, { color: theme.textMuted }]}>
            Loading athletes…
          </Text>
        ) : (
          <FlatList
            data={sorted}
            keyExtractor={(a) => a.athleteId}
            renderItem={renderAthlete}
            contentContainerStyle={styles.listContent}
            ListEmptyComponent={
              <Text style={[styles.status, { color: theme.textMuted }]}>
                No athletes for this position.
              </Text>
            }
          />
        )}
      </View>
    </>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  gate: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24 },
  gateHeading: { fontSize: 18, fontWeight: '700', marginBottom: 8 },
  sub: { fontSize: 12, paddingHorizontal: 16, paddingTop: 10 },
  slotRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    paddingHorizontal: 16,
    marginTop: 10,
  },
  slot: {
    borderWidth: 1,
    borderStyle: 'dashed',
    borderRadius: 8,
    paddingVertical: 6,
    paddingHorizontal: 12,
    alignItems: 'center',
    minWidth: 64,
  },
  slotDisabled: { opacity: 0.45 },
  slotLabel: { fontSize: 11, fontWeight: '700', letterSpacing: 0.8 },
  slotPlayer: { fontSize: 11, maxWidth: 96 },
  slotRemove: {
    position: 'absolute',
    top: -7,
    right: -7,
    width: 16,
    height: 16,
    borderRadius: 8,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  sortRow: { flexGrow: 0, marginTop: 10 },
  sortRowContent: { paddingHorizontal: 16, gap: 6 },
  sortChip: {
    borderWidth: 1,
    borderRadius: 14,
    paddingVertical: 4,
    paddingHorizontal: 10,
  },
  listContent: { padding: 16, gap: 10 },
  card: { borderWidth: 1, borderRadius: 10, padding: 12 },
  cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start' },
  cardIdentity: { flexShrink: 1, paddingRight: 8 },
  cardName: { fontSize: 15, fontWeight: '700' },
  cardPos: { fontSize: 11, fontWeight: '700' },
  cardTeam: { fontSize: 12, marginTop: 1 },
  addBtn: { borderWidth: 1, borderRadius: 6, paddingVertical: 4, paddingHorizontal: 10 },
  addBtnText: { fontSize: 12, fontWeight: '600' },
  cardMatchup: { fontSize: 12, marginTop: 6 },
  seasonRow: { flexDirection: 'row', marginTop: 5, alignItems: 'baseline' },
  seasonTag: { fontSize: 11, fontWeight: '700', width: 70 },
  seasonStats: { fontSize: 12, flexShrink: 1 },
  status: { padding: 16 },
});
