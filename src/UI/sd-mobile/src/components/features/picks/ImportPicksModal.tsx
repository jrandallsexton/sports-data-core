import React, { useEffect, useMemo, useState } from 'react';
import {
  Modal,
  View,
  FlatList,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Text } from '@/src/components/ui/AppText';
import { SegmentedControl } from '@/src/components/ui/SegmentedControl';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';

export interface ImportItemRow {
  contestId: string;
  franchiseSeasonId: string;
  team: string;
  matchupLabel: string;
}

export interface ImportSourceForModal {
  leagueId: string;
  name: string;
  items: ImportItemRow[];
}

interface Props {
  visible: boolean;
  sources: ImportSourceForModal[];
  importing: boolean;
  onClose: () => void;
  onImport: (sourceLeagueId: string, contestIds: string[]) => void;
}

/**
 * Import-picks sheet. A single import always draws from ONE source league; with
 * multiple candidates the user picks one (segmented control), then toggles the
 * checkbox list of that league's importable games (all checked by default).
 */
export function ImportPicksModal({ visible, sources, importing, onClose, onImport }: Props) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const [selectedSourceId, setSelectedSourceId] = useState<string | null>(
    sources[0]?.leagueId ?? null,
  );
  const currentSource =
    sources.find((s) => s.leagueId === selectedSourceId) ?? sources[0] ?? null;
  const items = currentSource?.items ?? [];

  const [selected, setSelected] = useState<Set<string>>(
    () => new Set(items.map((i) => i.contestId)),
  );

  // The modal stays mounted (so the sheet can animate in/out), so its initial
  // state can be stale — sources is often empty on first mount, before the
  // availability query resolves. Re-seed to the first source, all-checked, each
  // time the sheet opens. Keyed on `visible` only so a background sources refresh
  // while open can't wipe in-progress edits.
  useEffect(() => {
    if (!visible) return;
    const first = sources[0] ?? null;
    setSelectedSourceId(first?.leagueId ?? null);
    setSelected(new Set((first?.items ?? []).map((i) => i.contestId)));
    // eslint-disable-next-line react-hooks/exhaustive-deps -- seed only on open
  }, [visible]);

  // Switch source and re-seed the checkboxes to all of that source's picks. Done
  // on the user's action (not an effect) so a background refresh can't wipe edits.
  const changeSource = (leagueId: string) => {
    setSelectedSourceId(leagueId);
    const src = sources.find((s) => s.leagueId === leagueId);
    setSelected(new Set((src?.items ?? []).map((i) => i.contestId)));
  };

  const toggle = (contestId: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(contestId)) next.delete(contestId);
      else next.add(contestId);
      return next;
    });
  };

  const allSelected = items.length > 0 && items.every((i) => selected.has(i.contestId));
  const toggleAll = () =>
    setSelected(allSelected ? new Set() : new Set(items.map((i) => i.contestId)));

  // Submit only contests still present in the chosen source, so source and
  // contestIds can never mismatch.
  const chosenContestIds = useMemo(() => {
    const ids = new Set(items.map((i) => i.contestId));
    return [...selected].filter((id) => ids.has(id));
  }, [items, selected]);
  const count = chosenContestIds.length;

  const sourceOptions = sources.map((s) => ({ value: s.leagueId, label: s.name }));

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={importing ? undefined : onClose}
    >
      <View style={[styles.container, { backgroundColor: theme.background }]}>
        <View style={[styles.header, { borderBottomColor: theme.border }]}>
          <Text style={[styles.headerTitle, { color: theme.text }]}>Import picks</Text>
          <TouchableOpacity onPress={onClose} disabled={importing} hitSlop={12}>
            <Text style={[styles.closeText, { color: theme.textMuted }]}>✕</Text>
          </TouchableOpacity>
        </View>

        {sources.length > 1 && currentSource ? (
          <View style={styles.sourceRow}>
            <Text style={[styles.sourceLabel, { color: theme.textMuted }]}>Import from</Text>
            <SegmentedControl
              value={currentSource.leagueId}
              options={sourceOptions}
              onChange={changeSource}
              accessibilityLabel="Source league"
            />
          </View>
        ) : currentSource ? (
          <Text style={[styles.sourceHint, { color: theme.textMuted }]}>
            Copy your picks from {currentSource.name}. Uncheck any you don&rsquo;t want.
          </Text>
        ) : null}

        <TouchableOpacity
          style={styles.selectAll}
          onPress={toggleAll}
          disabled={importing || items.length === 0}
          accessibilityRole="checkbox"
          accessibilityLabel="Select all"
          accessibilityState={{ checked: allSelected, disabled: importing || items.length === 0 }}
        >
          <Ionicons
            name={allSelected ? 'checkbox' : 'square-outline'}
            size={20}
            color={theme.tint}
          />
          <Text style={[styles.selectAllText, { color: theme.textMuted }]}>Select all</Text>
        </TouchableOpacity>

        <FlatList
          data={items}
          keyExtractor={(item) => item.contestId}
          renderItem={({ item }) => {
            const checked = selected.has(item.contestId);
            return (
              <TouchableOpacity
                style={[styles.row, { borderBottomColor: theme.border }]}
                onPress={() => toggle(item.contestId)}
                disabled={importing}
                accessibilityRole="checkbox"
                accessibilityLabel={`${item.matchupLabel}, pick ${item.team}`}
                accessibilityState={{ checked, disabled: importing }}
              >
                <Ionicons
                  name={checked ? 'checkbox' : 'square-outline'}
                  size={22}
                  color={checked ? theme.tint : theme.textMuted}
                />
                <View style={styles.rowText}>
                  <Text style={[styles.rowMatchup, { color: theme.text }]}>
                    {item.matchupLabel}
                  </Text>
                </View>
                <Text style={[styles.rowTeam, { color: theme.tint }]} numberOfLines={1}>
                  {item.team}
                </Text>
              </TouchableOpacity>
            );
          }}
          contentContainerStyle={styles.list}
        />

        <View style={[styles.footer, { borderTopColor: theme.border }]}>
          <TouchableOpacity
            style={[styles.button, styles.cancel, { borderColor: theme.border }]}
            onPress={onClose}
            disabled={importing}
          >
            <Text style={[styles.buttonText, { color: theme.text }]}>Cancel</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.button,
              styles.confirm,
              { backgroundColor: theme.tint },
              (importing || count === 0) && styles.disabled,
            ]}
            onPress={() => currentSource && onImport(currentSource.leagueId, chosenContestIds)}
            disabled={importing || count === 0}
          >
            <Text style={[styles.buttonText, { color: theme.textOnAccent }]}>
              {importing ? 'Importing…' : `Import ${count} pick${count === 1 ? '' : 's'}`}
            </Text>
          </TouchableOpacity>
        </View>

        {importing && (
          <View style={styles.savingOverlay}>
            <ActivityIndicator size="small" color={theme.tint} />
          </View>
        )}
      </View>
    </Modal>
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
  headerTitle: { fontSize: 18, fontWeight: '700' },
  closeText: { fontSize: 20, fontWeight: '600' },
  sourceRow: { paddingHorizontal: 16, paddingTop: 14, gap: 8 },
  sourceLabel: { fontSize: 13, fontWeight: '600' },
  sourceHint: { paddingHorizontal: 16, paddingTop: 14, fontSize: 14, lineHeight: 20 },
  selectAll: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 16,
    paddingVertical: 12,
  },
  selectAllText: { fontSize: 13, fontWeight: '600' },
  list: { paddingHorizontal: 16, paddingBottom: 16 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    paddingVertical: 12,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  rowText: { flex: 1 },
  rowMatchup: { fontSize: 15 },
  rowTeam: { fontSize: 15, fontWeight: '700', maxWidth: 120 },
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
  confirm: {},
  disabled: { opacity: 0.5 },
  buttonText: { fontSize: 15, fontWeight: '700' },
  savingOverlay: {
    ...StyleSheet.absoluteFillObject,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(0,0,0,0.15)',
  },
});
