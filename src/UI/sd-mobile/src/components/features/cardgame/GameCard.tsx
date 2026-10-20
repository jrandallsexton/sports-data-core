import React, { memo } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { DB, WIN_SHORT } from '@/src/utils/cardgame/engine';
import type { CardInst } from '@/src/utils/cardgame/types';
import { CATEGORY_COLOR, CG, FONT_DISPLAY, FONT_MONO } from './palette';

// Compact card face for the hand grid and the snap row: cost, name, then
// yardage (play cards) or the start of the rules (everything else); the full
// rules are a long-press away. Type sizes scale with the card's width so the
// face reads the same at 3, 4, or 5 across. Card faces use React Native's
// Text, not AppText: they're sized to fit, like a printed card, so the app's
// text-size preference doesn't apply (same opt-out as the Wordmark).

// Space for the playable/selected ring outside the card face.
export const RING = 3;

export type CardMark = 'legal' | 'selected' | 'dim' | null;

interface Props {
  inst: CardInst;
  /** Outer size, ring included. */
  width: number;
  height: number;
  mark?: CardMark;
  onPress?: (inst: CardInst) => void;
  onLongPress?: (inst: CardInst) => void;
}

function GameCardBase({ inst, width, height, mark = null, onPress, onLongPress }: Props) {
  const c = DB[inst.id];
  if (!c) return null;
  const w = width - RING * 2;
  const cat = CATEGORY_COLOR[c.category];
  const showCost = !!c.cost && c.category !== 'read';
  const costLabel = c.cost?.kind === 'all' ? 'ALL' : String(c.cost?.value ?? '');
  const foot = inst.base ? 'Base' : c.category === 'read' ? 'Read' : c.window === 'commit' ? 'Play' : WIN_SHORT[c.window ?? ''] ?? '';
  const rules = c.category === 'read' ? c.rulesText.replace(/^Commit face-down\.\s*/, '') : c.rulesText;
  const circle = Math.round(w * 0.23);
  const costFont = w * (c.cost?.kind === 'all' ? 0.09 : 0.14);
  const ringColor = mark === 'legal' ? CG.gold : mark === 'selected' ? CG.select : 'transparent';

  return (
    <Pressable
      onPress={onPress ? () => onPress(inst) : undefined}
      onLongPress={onLongPress ? () => onLongPress(inst) : undefined}
      delayLongPress={450}
      accessibilityRole="button"
      accessibilityLabel={`${c.name}${mark === 'legal' ? ', playable' : ''}`}
      style={[
        styles.ring,
        { width, height, borderColor: ringColor, opacity: mark === 'dim' ? 0.4 : 1 },
        mark === 'selected' && styles.lifted,
      ]}
    >
      <View style={styles.card}>
        <View style={[styles.band, { height: Math.round(w * 0.27), backgroundColor: cat }]}>
          {showCost ? (
            <View style={[styles.cost, { width: circle, height: circle, borderRadius: circle / 2 }]}>
              <Text
                allowFontScaling={false}
                // No lineHeight: a line box shorter than the font's natural one (ascent 1.036 +
                // descent 0.333 em) is laid out differently on iOS and Android, and pushed the
                // digit up on device. With natural metrics the digit (0 to ~0.79 em) sits
                // 0.04 em above the box's center on every platform; the nudge corrects that.
                style={[styles.costText, { fontSize: costFont, transform: [{ translateY: costFont * 0.04 }] }]}
              >
                {costLabel}
              </Text>
            </View>
          ) : null}
        </View>
        <View style={[styles.name, { borderBottomColor: cat, minHeight: w * 0.12 * 1.1 * 2 + 6 }]}>
          <Text
            allowFontScaling={false}
            numberOfLines={2}
            adjustsFontSizeToFit
            minimumFontScale={0.75}
            style={[styles.nameText, { fontSize: w * 0.12, lineHeight: w * 0.12 * 1.1 }]}
          >
            {c.name.toUpperCase()}
          </Text>
        </View>
        {c.play ? (
          <View style={styles.yards}>
            <Text allowFontScaling={false} style={[styles.yardsText, { fontSize: w * 0.28, lineHeight: w * 0.3 }]}>
              {c.play.yards ?? 'TD?'}
            </Text>
            <View style={styles.typeRow}>
              <Text allowFontScaling={false} style={[styles.mono, { fontSize: w * 0.1 }]}>
                {c.play.type.toUpperCase()}
              </Text>
              {c.play.readYards != null ? (
                <View style={styles.readBadge}>
                  <Text allowFontScaling={false} style={[styles.mono, { fontSize: w * 0.09 }]}>
                    READ {c.play.readYards}
                  </Text>
                </View>
              ) : null}
            </View>
          </View>
        ) : (
          <Text
            allowFontScaling={false}
            numberOfLines={4}
            style={[styles.rules, { fontSize: w * 0.1, lineHeight: w * 0.125 }]}
          >
            {rules}
          </Text>
        )}
        <Text allowFontScaling={false} style={[styles.mono, styles.foot, { fontSize: w * 0.1 }]}>
          {foot.toUpperCase()}
        </Text>
      </View>
    </Pressable>
  );
}

export const GameCard = memo(GameCardBase);

const styles = StyleSheet.create({
  ring: { padding: 1, borderWidth: 2, borderRadius: 10 },
  lifted: { transform: [{ translateY: -3 }] },
  card: {
    flex: 1,
    backgroundColor: CG.paper,
    borderWidth: 2,
    borderColor: CG.ink,
    borderRadius: 7,
    overflow: 'hidden',
  },
  band: { position: 'relative' },
  cost: {
    position: 'absolute',
    top: 2,
    left: 2,
    backgroundColor: CG.chalk,
    borderWidth: 1.5,
    borderColor: CG.ink,
    alignItems: 'center',
    justifyContent: 'center',
  },
  costText: { fontFamily: FONT_DISPLAY, color: CG.ink, textAlign: 'center', includeFontPadding: false },
  name: {
    backgroundColor: CG.ink,
    borderBottomWidth: 2,
    paddingHorizontal: 4,
    paddingVertical: 3,
    justifyContent: 'flex-end',
  },
  nameText: { fontFamily: FONT_DISPLAY, color: CG.chalk, includeFontPadding: false },
  yards: { paddingHorizontal: 4, paddingTop: 2 },
  yardsText: { fontFamily: FONT_DISPLAY, color: CG.ink, includeFontPadding: false },
  typeRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 3, flexWrap: 'wrap' },
  readBadge: { borderWidth: 1, borderColor: CG.ink, borderRadius: 4, paddingHorizontal: 3, backgroundColor: CG.chalk },
  mono: { fontFamily: FONT_MONO, color: CG.ink, includeFontPadding: false },
  rules: { color: CG.ink, paddingHorizontal: 4, paddingTop: 3, flexShrink: 1 },
  foot: { marginTop: 'auto', paddingHorizontal: 4, paddingBottom: 3, color: CG.inkSoft },
});
