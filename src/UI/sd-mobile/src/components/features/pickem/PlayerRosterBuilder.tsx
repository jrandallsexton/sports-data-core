import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  View,
  StyleSheet,
  ScrollView,
  FlatList,
  TextInput,
  TouchableOpacity,
} from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { resolveSportLeague } from '@/src/utils/sportLinks';
import {
  getAthletesByPosition,
  getMyLineup,
  upsertSlot,
  clearSlot,
  type PickemAthlete,
  type Lineup,
  type LineupSlot,
} from '@/src/services/api/playerPickemApi';
import {
  SLOT_DEFS,
  eligiblePositions,
  canAssign,
  isRostered,
  type Roster,
} from '@/src/utils/pickem/rosterLogic';
import {
  statPartsFor,
  seasonLine,
  sortAthletes,
  filterAthletes,
  filterByOpponent,
  NAME_SORT,
  type SortDescriptor,
} from '@/src/utils/pickem/athleteStats';

const OPP_DEF_LABEL: Record<string, string> = {
  QB: 'Pass Alw/G',
  RB: 'Rush Alw/G',
  WR: 'Pass Alw/G',
  TE: 'Pass Alw/G',
  K: 'Pts Alw/G',
  FLEX: 'Def/G',
};

/** Saved lineup, keyed by slot id. */
type SlotMap = Record<string, LineupSlot | undefined>;

function slotsFromLineup(lineup: Lineup | null | undefined): SlotMap {
  return Object.fromEntries((lineup?.slots ?? []).map((s) => [s.slotId, s]));
}

/** First useful message out of an API validation failure, else a fallback. */
function errorMessage(err: unknown, fallback: string): string {
  const errors = (err as { response?: { data?: { errors?: { errorMessage?: string }[] } } })
    ?.response?.data?.errors;
  return Array.isArray(errors) && errors[0]?.errorMessage ? errors[0].errorMessage : fallback;
}

export interface PlayerRosterBuilderProps {
  leagueId: string;
  seasonYear: number;
  week: number;
  /** Backend Sport enum ("FootballNcaa" | "FootballNfl"). */
  sport: string;
}

/**
 * Player Pick'em roster builder: the mobile counterpart of sd-ui's
 * PlayerRosterBuilder, rendered by the Picks tab for Player Pick'em leagues.
 * The lineup lives on the server (per league/user/week, per-slot locks the
 * server enforces); the web's mirrored table columns become stacked
 * current/previous-season lines on a card, and column-header sorting
 * becomes a chip row.
 */
export function PlayerRosterBuilder({ leagueId, seasonYear, week, sport }: PlayerRosterBuilderProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const sportLeague = resolveSportLeague(sport);

  const [roster, setRoster] = useState<SlotMap>({});
  const [totalPoints, setTotalPoints] = useState<number | null>(null);
  const [rosterLoading, setRosterLoading] = useState(true);
  const [saveError, setSaveError] = useState<string | null>(null);
  // Bumping this re-reads the lineup WITHOUT the loading/reset churn of a
  // league or week change, so slot points and the total come from one
  // server response after every save.
  const [refreshTick, setRefreshTick] = useState(0);

  const [activeSlotId, setActiveSlotId] = useState('QB');
  const [athletes, setAthletes] = useState<PickemAthlete[]>([]);
  const [loading, setLoading] = useState(false);
  const [sort, setSort] = useState<SortDescriptor>(NAME_SORT);
  const [filterText, setFilterText] = useState('');
  const [opponentText, setOpponentText] = useState('');

  // A league or week change shows the loading state and starts clean.
  useEffect(() => {
    setRoster({});
    setTotalPoints(null);
    setSaveError(null);
    setRosterLoading(true);
  }, [leagueId, seasonYear, week]);

  useEffect(() => {
    let ignore = false;
    getMyLineup(leagueId, seasonYear, week)
      .then((lineup) => {
        if (ignore) return;
        setRoster(slotsFromLineup(lineup));
        setTotalPoints(lineup?.totalPoints ?? null);
      })
      .catch(() => {
        if (!ignore) setSaveError('Could not load your roster.');
      })
      .finally(() => {
        if (!ignore) setRosterLoading(false);
      });
    return () => {
      ignore = true;
    };
  }, [leagueId, seasonYear, week, refreshTick]);

  const positions = useMemo(() => eligiblePositions(activeSlotId), [activeSlotId]);
  const parts = useMemo(() => statPartsFor(activeSlotId, positions), [activeSlotId, positions]);

  // Stat sets differ per slot: slot and league changes reset sort and filters.
  useEffect(() => {
    setSort(NAME_SORT);
    setFilterText('');
    setOpponentText('');
  }, [activeSlotId, leagueId]);

  useEffect(() => {
    if (positions.length === 0 || !sportLeague) return;
    let ignore = false;
    setLoading(true);
    Promise.all(
      positions.map((pos) =>
        getAthletesByPosition(pos, seasonYear, week, sportLeague.sport, sportLeague.league),
      ),
    )
      .then((responses) => {
        if (!ignore) setAthletes(responses.flatMap((r) => r.athletes));
      })
      .catch(() => {
        if (!ignore) setAthletes([]);
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });
    return () => {
      ignore = true;
    };
  }, [positions, sportLeague?.sport, sportLeague?.league, seasonYear, week]);

  const sorted = useMemo(
    () =>
      sortAthletes(
        filterByOpponent(filterAthletes(athletes, filterText), opponentText),
        sort,
        parts,
      ),
    [athletes, filterText, opponentText, sort, parts],
  );

  const toggleSort = useCallback((key: string) => {
    setSort((prev) =>
      prev.key === key ? { key, dir: prev.dir === 'desc' ? 'asc' : 'desc' } : { key, dir: 'desc' },
    );
  }, []);

  // rosterLogic's checks read only athleteId/position, which saved slots
  // carry, so the saved lineup stands in for its Roster type.
  const asRoster = roster as unknown as Roster;

  const handleAssign = async (athlete: PickemAthlete) => {
    // Client-side pre-checks (eligibility, duplicates) fail fast; the server
    // re-validates everything INCLUDING locks, which only it can judge.
    if (!canAssign(asRoster, activeSlotId, athlete)) return;
    setSaveError(null);
    try {
      const saved = await upsertSlot(leagueId, seasonYear, week, activeSlotId, athlete);
      setRoster((prev) => ({ ...prev, [activeSlotId]: saved }));
      setRefreshTick((t) => t + 1);
    } catch (err) {
      setSaveError(errorMessage(err, 'Could not save that pick.'));
    }
  };

  const handleRemove = async (slotId: string) => {
    setSaveError(null);
    try {
      await clearSlot(leagueId, seasonYear, week, slotId);
      setRoster((prev) => {
        const next = { ...prev };
        delete next[slotId];
        return next;
      });
      setRefreshTick((t) => t + 1);
    } catch (err) {
      setSaveError(errorMessage(err, 'Could not clear that slot.'));
    }
  };

  const activeSlot = SLOT_DEFS.find((s) => s.id === activeSlotId);
  const occupant = roster[activeSlotId];
  const occupantLocked = occupant?.isLocked === true;
  const oppDefLabel = OPP_DEF_LABEL[activeSlot?.id === 'FLEX' ? 'FLEX' : positions[0]];

  const renderAthlete = ({ item: a }: { item: PickemAthlete }) => {
    const rostered = isRostered(asRoster, a.athleteId);
    const disabled = rostered || occupantLocked;
    const label = rostered
      ? 'Rostered'
      : occupantLocked
        ? 'Locked'
        : occupant && occupant.athleteId !== a.athleteId
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
                  {'  '}
                  {a.position}
                </Text>
              ) : null}
            </Text>
            <Text style={[styles.cardTeam, { color: theme.textMuted }]}>{a.teamShortName ?? a.teamName}</Text>
          </View>
          <TouchableOpacity
            style={[styles.addBtn, { borderColor: disabled ? theme.border : theme.tint }]}
            disabled={disabled}
            onPress={() => handleAssign(a)}
            accessibilityRole="button"
            accessibilityLabel={`${label} ${a.firstName} ${a.lastName}`}
          >
            <Text style={[styles.addBtnText, { color: disabled ? theme.textMuted : theme.tint }]}>
              {label}
            </Text>
          </TouchableOpacity>
        </View>

        <Text style={[styles.cardMatchup, { color: theme.textMuted }]}>
          {a.opponentName ? `vs ${a.opponentShortName ?? a.opponentName}` : 'BYE'}
          {a.opponentDefPerGame != null
            ? // Per-row label: on FLEX the number's meaning depends on the
              // athlete's position (rush vs pass allowed).
              ` · Opp ${a.opponentDefPerGame.toFixed(1)} ${OPP_DEF_LABEL[a.position]}`
            : ''}
        </Text>

        {/* Current over previous season in the same stat order: the mobile
            translation of the web grid's mirrored columns. */}
        <View style={styles.seasonRow}>
          <Text style={[styles.seasonTag, { color: theme.text }]}>
            {a.currentSeason
              ? `'${a.currentSeason.seasonYear % 100} · ${a.currentSeason.gamesPlayed} G`
              : `'${seasonYear % 100} · —`}
          </Text>
          <Text style={[styles.seasonStats, { color: theme.text }]} numberOfLines={1}>
            {a.currentSeason ? seasonLine(parts, a.currentSeason, a) : 'No games yet'}
          </Text>
        </View>
        <View style={styles.seasonRow}>
          <Text style={[styles.seasonTag, { color: theme.textMuted }]}>
            {a.previousSeason
              ? `'${a.previousSeason.seasonYear % 100} · ${a.previousSeason.gamesPlayed} G`
              : `'${(seasonYear - 1) % 100} · —`}
          </Text>
          <Text style={[styles.seasonStats, { color: theme.textMuted }]} numberOfLines={1}>
            {a.previousSeason ? seasonLine(parts, a.previousSeason, a) : 'No prior season'}
          </Text>
        </View>
      </View>
    );
  };

  return (
    <View style={styles.container}>
      <Text style={[styles.sub, { color: theme.textMuted }]}>
        {`Week ${week} · ${seasonYear}`}
        {totalPoints != null && totalPoints !== 0 ? (
          <Text style={[styles.total, { color: theme.tint }]}>{` · ${totalPoints.toFixed(1)} pts`}</Text>
        ) : null}
      </Text>

      {/* Wrapping grid, not a horizontal scroller: every slot visible at once
          so nothing about the lineup shape hides off-screen. */}
      <View style={styles.slotRow}>
        {SLOT_DEFS.map((slot) => {
          const filled = roster[slot.id];
          const isActive = slot.id === activeSlotId;
          return (
            <TouchableOpacity
              key={slot.id}
              disabled={slot.disabled}
              onPress={() => setActiveSlotId(slot.id)}
              accessibilityRole="button"
              accessibilityLabel={`${slot.label} slot`}
              style={[
                styles.slot,
                { borderColor: theme.border },
                filled && { borderColor: theme.tint, borderStyle: 'solid' },
                isActive && { borderColor: theme.tint, borderWidth: 2 },
                slot.disabled && styles.slotDisabled,
              ]}
            >
              <Text style={[styles.slotLabel, { color: filled ? theme.tint : theme.textMuted }]}>
                {slot.label}
              </Text>
              <Text
                style={[styles.slotPlayer, { color: filled ? theme.text : theme.textMuted }]}
                numberOfLines={1}
              >
                {filled
                  ? `${filled.firstName.charAt(0)}. ${filled.lastName}`
                  : slot.disabled
                    ? 'Soon'
                    : '—'}
              </Text>
              {filled && filled.points != null ? (
                <Text style={[styles.slotPoints, { color: theme.tint }]}>
                  {filled.points.toFixed(1)}
                </Text>
              ) : null}
              {filled && !filled.isLocked ? (
                <TouchableOpacity
                  style={[styles.slotRemove, { backgroundColor: theme.card, borderColor: theme.border }]}
                  accessibilityLabel={`Remove ${filled.firstName} ${filled.lastName}`}
                  onPress={() => handleRemove(slot.id)}
                >
                  <Text style={{ color: theme.textMuted, fontSize: 10, lineHeight: 12 }}>✕</Text>
                </TouchableOpacity>
              ) : null}
            </TouchableOpacity>
          );
        })}
      </View>

      {rosterLoading ? (
        <Text style={[styles.status, { color: theme.textMuted }]}>Loading your roster…</Text>
      ) : null}
      {saveError ? (
        <Text style={[styles.status, { color: theme.error }]} accessibilityRole="alert">
          {saveError}
        </Text>
      ) : null}

      <View style={styles.filterRow}>
        <TextInput
          style={[styles.filterInput, { borderColor: theme.border, color: theme.text, backgroundColor: theme.card }]}
          placeholder="Player or team…"
          placeholderTextColor={theme.textMuted}
          value={filterText}
          onChangeText={setFilterText}
          autoCorrect={false}
          clearButtonMode="while-editing"
        />
        {/* The matchup hunt: "show me the RBs playing UMass this weekend." */}
        <TextInput
          style={[styles.filterInput, { borderColor: theme.border, color: theme.text, backgroundColor: theme.card }]}
          placeholder="Opponent…"
          placeholderTextColor={theme.textMuted}
          value={opponentText}
          onChangeText={setOpponentText}
          autoCorrect={false}
          clearButtonMode="while-editing"
        />
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
          // No opponent-defense sort on FLEX: rush vs pass yds allowed are
          // different units, so a cross-position ranking would lie.
          ...(activeSlotId === 'FLEX' ? [] : [{ key: 'oppDef', label: `Opp ${oppDefLabel}` }]),
        ].map((chip) => {
          const active = sort.key === chip.key;
          const arrow =
            active && chip.key !== 'name' ? (sort.dir === 'asc' ? ' ▲' : ' ▼') : '';
          return (
            <TouchableOpacity
              key={chip.key}
              onPress={() => (chip.key === 'name' ? setSort(NAME_SORT) : toggleSort(chip.key))}
              style={[styles.sortChip, { borderColor: active ? theme.tint : theme.border }]}
            >
              <Text style={{ color: active ? theme.tint : theme.textMuted, fontSize: 12, fontWeight: '600' }}>
                {chip.label}
                {arrow}
              </Text>
            </TouchableOpacity>
          );
        })}
      </ScrollView>

      {loading ? (
        <Text style={[styles.status, { color: theme.textMuted }]}>Loading athletes…</Text>
      ) : (
        <FlatList
          data={sorted}
          keyExtractor={(a) => a.athleteId}
          renderItem={renderAthlete}
          contentContainerStyle={styles.listContent}
          ListEmptyComponent={
            <Text style={[styles.status, { color: theme.textMuted }]}>No athletes for this position.</Text>
          }
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  sub: { fontSize: 12, paddingHorizontal: 16, paddingTop: 10 },
  total: { fontWeight: '700' },
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
  slotPoints: { fontSize: 10, fontWeight: '700' },
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
  filterRow: {
    flexDirection: 'row',
    gap: 8,
    marginHorizontal: 16,
    marginTop: 10,
  },
  filterInput: {
    flex: 1,
    borderWidth: 1,
    borderRadius: 8,
    paddingVertical: 7,
    paddingHorizontal: 10,
    fontSize: 13,
  },
  // flexShrink 0: a ScrollView shrinks by default, and under the Picks tab's
  // selector the column runs out of height, which squeezed the chips to a
  // sliver.
  sortRow: { flexGrow: 0, flexShrink: 0, marginTop: 10 },
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
  status: { paddingHorizontal: 16, paddingTop: 8 },
});
