import React, { useEffect, useMemo, useState } from 'react';
import {
  Modal,
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  FlatList,
  TextInput,
  ActivityIndicator,
} from 'react-native';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Colors, getTheme } from '@/constants/Colors';
import { DEFAULT_TIMEZONE } from '@/src/utils/timeUtils';

/**
 * Curated short-list mirrors the web SettingsPage. Order matters: Eastern,
 * Central, Mountain, Phoenix (no DST), Pacific, Alaska, Hawaii. Anything
 * outside this list is reachable via the "Other…" entry which expands the
 * full Intl.supportedValuesOf("timeZone") catalog.
 */
const CURATED_TIMEZONES: { value: string; label: string }[] = [
  { value: 'America/New_York', label: 'Eastern (New York)' },
  { value: 'America/Chicago', label: 'Central (Chicago)' },
  { value: 'America/Denver', label: 'Mountain (Denver)' },
  { value: 'America/Phoenix', label: 'Mountain - no DST (Phoenix)' },
  { value: 'America/Los_Angeles', label: 'Pacific (Los Angeles)' },
  { value: 'America/Anchorage', label: 'Alaska (Anchorage)' },
  { value: 'Pacific/Honolulu', label: 'Hawaii (Honolulu)' },
];

const OTHER_VALUE = '__other__';

function getAllIanaZones(): string[] {
  // Intl.supportedValuesOf is available on Hermes with full ICU (Expo 55+).
  // Fall back to the curated list if a host strips it.
  const intl = Intl as unknown as { supportedValuesOf?: (k: string) => string[] };
  if (typeof intl.supportedValuesOf === 'function') {
    try {
      return intl.supportedValuesOf('timeZone');
    } catch {
      return [];
    }
  }
  return [];
}

interface TimezonePickerModalProps {
  visible: boolean;
  onClose: () => void;
  /** The currently effective timezone to highlight as selected. */
  currentTimezone: string;
  /** Called when the user taps a zone. Returns a Promise so the modal can
   *  show a saving state and stay open until the save resolves. */
  onSelect: (timezone: string) => Promise<void> | void;
  /** Optional message to surface (e.g. "Saved." or "Could not save."). */
  message?: string;
}

export function TimezonePickerModal({
  visible,
  onClose,
  currentTimezone,
  onSelect,
  message,
}: TimezonePickerModalProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const allZones = useMemo(() => getAllIanaZones(), []);
  const isCurated = CURATED_TIMEZONES.some((z) => z.value === currentTimezone);

  // Auto-flip to the full list when the user has a non-curated zone (e.g.
  // Europe/London) so they can see and change it. Initial state covers the
  // first mount (no first-frame flash); the effect below re-evaluates each
  // time the modal becomes visible so closing + re-opening with a still-
  // non-curated zone re-flips. Critically, expanded is just `showAll` so
  // the footer's `setShowAll(false)` actually collapses the view.
  const [showAll, setShowAll] = useState(() => !isCurated && allZones.length > 0);
  const [filter, setFilter] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (visible) {
      setShowAll(!isCurated && allZones.length > 0);
    }
  }, [visible, isCurated, allZones.length]);

  const expanded = showAll;

  const filteredZones = useMemo(() => {
    if (!expanded) return [];
    const q = filter.trim().toLowerCase();
    if (!q) return allZones;
    return allZones.filter((z) => z.toLowerCase().includes(q));
  }, [expanded, allZones, filter]);

  const handleSelect = async (tz: string) => {
    if (!tz || saving) return;
    setSaving(true);
    try {
      await onSelect(tz);
    } finally {
      setSaving(false);
    }
  };

  const handleClose = () => {
    if (saving) return;
    setShowAll(false);
    setFilter('');
    onClose();
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={handleClose}
    >
      <View style={[styles.container, { backgroundColor: theme.background }]}>
        <View style={[styles.header, { borderBottomColor: theme.border }]}>
          <View style={styles.headerLeft} />
          <Text style={[styles.headerTitle, { color: theme.text }]}>Timezone</Text>
          <TouchableOpacity onPress={handleClose} style={styles.closeBtn} hitSlop={12}>
            <Text style={[styles.closeText, { color: theme.textMuted }]}>✕</Text>
          </TouchableOpacity>
        </View>

        {message ? (
          <View style={[styles.messageBar, { backgroundColor: theme.card, borderBottomColor: theme.border }]}>
            <Text style={[styles.messageText, { color: theme.textMuted }]}>{message}</Text>
          </View>
        ) : null}

        {expanded ? (
          <View style={[styles.searchRow, { borderBottomColor: theme.border }]}>
            <TextInput
              style={[styles.searchInput, { color: theme.text, backgroundColor: theme.card, borderColor: theme.border }]}
              placeholder="Search timezone (e.g. Chicago, GMT)…"
              placeholderTextColor={theme.textMuted}
              autoCorrect={false}
              autoCapitalize="none"
              value={filter}
              onChangeText={setFilter}
              editable={!saving}
            />
          </View>
        ) : null}

        {expanded ? (
          allZones.length === 0 ? (
            <View style={styles.emptyContainer}>
              <Text style={[styles.emptyText, { color: theme.textMuted }]}>
                Full timezone list isn't available on this device. Pick from the curated list instead.
              </Text>
              <TouchableOpacity
                onPress={() => setShowAll(false)}
                style={[styles.linkBtn, { borderColor: Colors.brand.navy }]}
              >
                <Text style={[styles.linkBtnText, { color: Colors.brand.navy }]}>Back to curated list</Text>
              </TouchableOpacity>
            </View>
          ) : (
            <FlatList
              data={filteredZones}
              keyExtractor={(z) => z}
              keyboardShouldPersistTaps="handled"
              renderItem={({ item }) => {
                const selected = item === currentTimezone;
                return (
                  <TouchableOpacity
                    style={[
                      styles.row,
                      { borderBottomColor: theme.separator },
                      selected && { backgroundColor: theme.card },
                    ]}
                    onPress={() => handleSelect(item)}
                    disabled={saving}
                    activeOpacity={0.6}
                  >
                    <Text
                      style={[
                        styles.rowLabel,
                        { color: selected ? theme.tint : theme.text },
                        selected && styles.rowSelected,
                      ]}
                    >
                      {item}
                    </Text>
                    {selected ? (
                      <Text style={[styles.checkmark, { color: theme.tint }]}>✓</Text>
                    ) : null}
                  </TouchableOpacity>
                );
              }}
              ListFooterComponent={
                <TouchableOpacity
                  onPress={() => {
                    setShowAll(false);
                    setFilter('');
                  }}
                  style={styles.footerLink}
                  disabled={saving}
                >
                  <Text style={[styles.footerLinkText, { color: theme.tint }]}>
                    Back to curated list
                  </Text>
                </TouchableOpacity>
              }
            />
          )
        ) : (
          <FlatList
            data={CURATED_TIMEZONES}
            keyExtractor={(item) => item.value}
            renderItem={({ item }) => {
              const selected = item.value === currentTimezone;
              return (
                <TouchableOpacity
                  style={[
                    styles.row,
                    { borderBottomColor: theme.separator },
                    selected && { backgroundColor: theme.card },
                  ]}
                  onPress={() => handleSelect(item.value)}
                  disabled={saving}
                  activeOpacity={0.6}
                >
                  <View style={{ flex: 1 }}>
                    <Text
                      style={[
                        styles.rowLabel,
                        { color: selected ? theme.tint : theme.text },
                        selected && styles.rowSelected,
                      ]}
                    >
                      {item.label}
                    </Text>
                    <Text style={[styles.rowSub, { color: theme.textMuted }]}>{item.value}</Text>
                  </View>
                  {selected ? (
                    <Text style={[styles.checkmark, { color: theme.tint }]}>✓</Text>
                  ) : null}
                </TouchableOpacity>
              );
            }}
            ListFooterComponent={
              <TouchableOpacity
                onPress={() => {
                  if (allZones.length > 0) setShowAll(true);
                }}
                style={styles.footerLink}
                disabled={saving || allZones.length === 0}
              >
                <Text
                  style={[
                    styles.footerLinkText,
                    { color: allZones.length > 0 ? theme.tint : theme.textMuted },
                  ]}
                >
                  {allZones.length > 0 ? 'Other…' : 'Other (unavailable)'}
                </Text>
              </TouchableOpacity>
            }
          />
        )}

        {saving ? (
          <View style={[styles.savingOverlay, { backgroundColor: theme.background }]}>
            <ActivityIndicator size="small" color={Colors.brand.navy} />
            <Text style={[styles.savingText, { color: theme.textMuted }]}>Saving…</Text>
          </View>
        ) : null}
      </View>
    </Modal>
  );
}

export { CURATED_TIMEZONES, OTHER_VALUE, DEFAULT_TIMEZONE };

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
  headerLeft: { width: 32 },
  headerTitle: { fontSize: 17, fontWeight: '700' },
  closeBtn: { width: 32, alignItems: 'flex-end' },
  closeText: { fontSize: 17, fontWeight: '400' },

  messageBar: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  messageText: { fontSize: 13 },

  searchRow: {
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  searchInput: {
    height: 38,
    borderRadius: 8,
    borderWidth: StyleSheet.hairlineWidth,
    paddingHorizontal: 12,
    fontSize: 14,
  },

  row: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: StyleSheet.hairlineWidth,
    gap: 10,
  },
  rowLabel: { fontSize: 15 },
  rowSelected: { fontWeight: '700' },
  rowSub: { fontSize: 11, marginTop: 2 },
  checkmark: { fontSize: 16, fontWeight: '700' },

  footerLink: { padding: 16, alignItems: 'center' },
  footerLinkText: { fontSize: 14, fontWeight: '600' },

  emptyContainer: { padding: 24, alignItems: 'center', gap: 16 },
  emptyText: { fontSize: 14, textAlign: 'center', lineHeight: 20 },
  linkBtn: {
    borderWidth: 1.5,
    borderRadius: 10,
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  linkBtnText: { fontSize: 14, fontWeight: '600' },

  savingOverlay: {
    position: 'absolute',
    bottom: 0,
    left: 0,
    right: 0,
    paddingVertical: 16,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 10,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: 'rgba(0,0,0,0.1)',
    opacity: 0.95,
  },
  savingText: { fontSize: 13, fontWeight: '500' },
});
