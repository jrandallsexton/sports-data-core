import React from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import type { CardInst } from '@/src/utils/cardgame/types';
import type { HandSection } from '@/src/utils/cardgame/view';
import { GameCard, type CardMark } from './GameCard';
import { EMPTY_ROW, HAND_PAD_BOTTOM, HAND_PAD_TOP, HAND_PAD_X, SECTION_GAP, SECTION_HEADER, type HandLayout } from './handLayout';
import { CG, FONT_MONO } from './palette';

// The hand -- the hero of the screen. Every card in the active hands is visible
// at once; the grid size comes from fitHand(). The off-side hand collapses to a
// chip in the last section's header (no row of its own: on a real phone those
// 44px decide between 4 and 5 cards across) that opens a view-only sheet.

interface Props {
  active: HandSection[];
  other: HandSection[];
  layout: HandLayout;
  bottomInset: number;
  markOf: (inst: CardInst) => CardMark;
  onCardPress: (inst: CardInst) => void;
  onCardLongPress: (inst: CardInst) => void;
  onOpenOther: (index: number) => void;
}

export function HandArea({ active, other, layout, bottomInset, markOf, onCardPress, onCardLongPress, onOpenOther }: Props) {
  return (
    <ScrollView
      style={styles.scroll}
      scrollEnabled={!layout.fits}
      showsVerticalScrollIndicator={false}
      contentContainerStyle={[styles.content, { paddingBottom: HAND_PAD_BOTTOM + bottomInset }]}
    >
      {active.map((s, si) => (
        <View key={s.label}>
          <View style={styles.header}>
            <Text allowFontScaling={false} style={styles.headerText}>{`${s.label} · ${s.note}`.toUpperCase()}</Text>
            {si === active.length - 1 ? (
              <View style={styles.links}>
                {other.map((o, i) => (
                  <Pressable
                    key={o.label}
                    style={styles.other}
                    onPress={() => onOpenOther(i)}
                    hitSlop={8}
                    accessibilityRole="button"
                    accessibilityLabel={`View ${o.label}, ${o.cards.length} cards`}
                  >
                    <Text allowFontScaling={false} style={styles.otherText}>{`${o.short} ${o.cards.length} ›`.toUpperCase()}</Text>
                  </Pressable>
                ))}
              </View>
            ) : null}
          </View>
          {s.cards.length ? (
            <View style={[styles.grid, { gap: layout.gap }]}>
              {s.cards.map((inst) => (
                <GameCard
                  key={inst.uid}
                  inst={inst}
                  width={layout.cardW}
                  height={layout.cardH}
                  mark={markOf(inst)}
                  onPress={onCardPress}
                  onLongPress={onCardLongPress}
                />
              ))}
            </View>
          ) : (
            <Text allowFontScaling={false} style={styles.empty}>EMPTY</Text>
          )}
        </View>
      ))}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  scroll: { flex: 1 },
  content: { paddingHorizontal: HAND_PAD_X, paddingTop: HAND_PAD_TOP, gap: SECTION_GAP },
  header: { height: SECTION_HEADER, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', gap: 6 },
  links: { flexDirection: 'row', gap: 6, marginTop: -2 },
  headerText: { fontFamily: FONT_MONO, fontSize: 9.5, color: CG.inkSoft },
  grid: { flexDirection: 'row', flexWrap: 'wrap' },
  empty: { height: EMPTY_ROW, fontFamily: FONT_MONO, fontSize: 10, color: CG.inkSoft, paddingTop: 4 },
  other: { backgroundColor: CG.paper, borderWidth: 1.5, borderColor: CG.ink, borderRadius: 5, paddingHorizontal: 6, paddingVertical: 1 },
  otherText: { fontFamily: FONT_MONO, fontSize: 9.5, color: CG.ink },
});
