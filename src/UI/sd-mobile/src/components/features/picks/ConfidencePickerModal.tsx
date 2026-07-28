import React from 'react';
import { View, StyleSheet, Modal, TouchableOpacity, ScrollView } from 'react-native';

import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';

interface Props {
  visible: boolean;
  /** Points run 1..totalGames (web parity: ConfidencePicker.jsx). */
  totalGames: number;
  /** Values already assigned to other picks this week — disabled in the grid. */
  usedPoints: number[];
  /** The tapped pick's current value (re-assign flow): selectable + highlighted. */
  currentPoint: number | null;
  onSelect: (point: number) => void;
  onClose: () => void;
}

/**
 * Confidence-point picker — mobile mirror of web's ConfidencePicker overlay.
 * A league with confidence points requires every pick to carry a distinct
 * value from 1..N (N = games this week); values used on other picks are
 * disabled, the tapped pick's own value stays selectable so re-picking a team
 * doesn't dead-end.
 */
export function ConfidencePickerModal({
  visible,
  totalGames,
  usedPoints,
  currentPoint,
  onSelect,
  onClose,
}: Props) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const points = Array.from({ length: Math.max(totalGames, 1) }, (_, i) => i + 1);

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <TouchableOpacity
        style={styles.backdrop}
        activeOpacity={1}
        onPress={onClose}
        accessibilityRole="button"
        accessibilityLabel="Dismiss confidence picker"
      />
      <View style={[styles.sheet, { backgroundColor: theme.card, borderTopColor: theme.border }]}>
        <View style={[styles.header, { borderBottomColor: theme.border }]}>
          <Text style={[styles.title, { color: theme.text }]}>Select Confidence Points</Text>
          <TouchableOpacity onPress={onClose} hitSlop={12} accessibilityLabel="Close">
            <Text style={[styles.close, { color: theme.textMuted }]}>✕</Text>
          </TouchableOpacity>
        </View>
        <ScrollView contentContainerStyle={styles.grid}>
          {points.map((point) => {
            const isCurrent = point === currentPoint;
            const isUsed = usedPoints.includes(point) && !isCurrent;
            return (
              <TouchableOpacity
                key={point}
                style={[
                  styles.point,
                  { borderColor: theme.border, backgroundColor: theme.background },
                  isCurrent && { borderColor: theme.tint, backgroundColor: theme.tint },
                  isUsed && styles.pointUsed,
                ]}
                onPress={() => onSelect(point)}
                disabled={isUsed}
                accessibilityRole="button"
                accessibilityState={{ disabled: isUsed, selected: isCurrent }}
                accessibilityLabel={
                  isUsed
                    ? `${point} points, already used on another pick`
                    : `Assign ${point} points`
                }
              >
                <Text
                  style={[
                    styles.pointText,
                    { color: isCurrent ? theme.textOnAccent : theme.text },
                    isUsed && { color: theme.textMuted },
                  ]}
                >
                  {point}
                </Text>
              </TouchableOpacity>
            );
          })}
        </ScrollView>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.4)' },
  sheet: {
    borderTopWidth: StyleSheet.hairlineWidth,
    paddingBottom: 28,
    maxHeight: '65%',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  title: { fontSize: 16, fontWeight: '700' },
  close: { fontSize: 18, fontWeight: '600' },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
    padding: 16,
  },
  point: {
    width: 52,
    height: 44,
    borderRadius: 10,
    borderWidth: 1.5,
    alignItems: 'center',
    justifyContent: 'center',
  },
  pointUsed: { opacity: 0.45 },
  pointText: { fontSize: 16, fontWeight: '700' },
});
