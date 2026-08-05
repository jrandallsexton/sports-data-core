import React, { useEffect, useState } from 'react';
import { View, StyleSheet, TouchableOpacity } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';

import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';

// Dismissal survives sessions — a teaser that resurrects every launch is an
// ad, not an announcement. Mirrors web's localStorage key semantics.
const DISMISSED_KEY = 'playerPickemTeaserDismissed';

// Teaser lineup shape (illustrative — the real lineup shape is a v1 design
// decision, see docs/features/player-pickem.md). Counts, not duplicate
// boxes: density wins in an advertisement; the gameplay UI will render
// individual slots. Mirrors web's PlayerPickemTeaserCard.
const SLOTS: { label: string; filled?: boolean; count?: number }[] = [
  { label: 'QB', filled: true },
  { label: 'RB', count: 2 },
  { label: 'WR', count: 2 },
  { label: 'TE' },
  { label: 'FLEX' },
  { label: 'K' },
  { label: 'DEF' },
];

/**
 * "Coming Soon: Player Pick'em" teaser — the lineup-slot banner (design
 * chosen 2026-08-04; docs/features/player-pickem.md). The empty roster row
 * shows the game rather than describing it: QB filled in accent, the rest
 * dashed ("yours to pick"). No dates, no interactivity beyond dismiss.
 * Web parity: sd-ui's PlayerPickemTeaserCard (minus the pulse — static on
 * mobile). Renders nothing until the persisted dismissal has been read, so
 * a dismissed card never flashes on launch.
 */
export function PlayerPickemTeaserCard() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  // null = not yet hydrated from AsyncStorage.
  const [dismissed, setDismissed] = useState<boolean | null>(null);

  useEffect(() => {
    let cancelled = false;
    AsyncStorage.getItem(DISMISSED_KEY)
      .then((v) => {
        if (!cancelled) setDismissed(v === 'true');
      })
      .catch(() => {
        if (!cancelled) setDismissed(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  if (dismissed !== false) return null;

  const dismiss = () => {
    setDismissed(true);
    // Fire-and-forget — a failed write just means the teaser returns next
    // launch, which is harmless.
    AsyncStorage.setItem(DISMISSED_KEY, 'true').catch(() => {});
  };

  return (
    <View style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <TouchableOpacity
        onPress={dismiss}
        style={styles.dismiss}
        hitSlop={8}
        accessibilityRole="button"
        accessibilityLabel="Dismiss announcement"
      >
        <Text style={[styles.dismissText, { color: theme.textMuted }]}>✕</Text>
      </TouchableOpacity>

      <Text style={[styles.eyebrow, { color: theme.tint }]}>COMING SOON</Text>
      <Text style={[styles.title, { color: theme.text }]}>Player Pick’em</Text>
      <Text style={[styles.pitch, { color: theme.textMuted }]}>
        Pick any players, any week — no draft, no ownership. Know the matchups
        better than your league and prove it.
      </Text>

      <View style={styles.slotRow}>
        {SLOTS.map((slot) => (
          <View
            key={slot.label}
            style={[
              styles.slot,
              { borderColor: theme.textMuted },
              slot.filled && {
                borderStyle: 'solid',
                borderColor: theme.tint,
                backgroundColor: theme.accentSubtle,
              },
            ]}
          >
            <Text
              style={[
                styles.slotText,
                { color: slot.filled ? theme.tint : theme.textMuted },
              ]}
            >
              {slot.label}
              {slot.count ? <Text style={styles.slotCount}> ×{slot.count}</Text> : null}
            </Text>
          </View>
        ))}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
  },
  dismiss: {
    position: 'absolute',
    top: 10,
    right: 12,
    zIndex: 1,
    padding: 4,
  },
  dismissText: { fontSize: 14, fontWeight: '600' },
  eyebrow: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
  },
  title: {
    fontSize: 18,
    fontWeight: '800',
    marginTop: 2,
    marginBottom: 2,
  },
  pitch: {
    fontSize: 13,
    marginBottom: 12,
  },
  slotRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  slot: {
    borderWidth: 1,
    borderStyle: 'dashed',
    borderRadius: 8,
    paddingVertical: 6,
    paddingHorizontal: 12,
    minWidth: 34,
    alignItems: 'center',
  },
  slotText: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.8,
  },
  slotCount: {
    fontWeight: '400',
    letterSpacing: 0,
  },
});
