import React, { useRef } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Switch, View } from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { DB } from '@/src/utils/cardgame/engine';
import type { CardInst, LogEntry } from '@/src/utils/cardgame/types';
import { LogLine } from './SnapRow';
import { CG, FONT_DISPLAY, FONT_MONO } from './palette';

// Bottom sheets: the full play-by-play (with the Confirm plays setting), and
// a view-only list of an off-side hand.

interface SheetProps {
  title: string;
  bottomInset: number;
  onClose: () => void;
  children: React.ReactNode;
}

function Sheet({ title, bottomInset, onClose, children }: SheetProps) {
  return (
    <Modal transparent animationType="slide" onRequestClose={onClose}>
      <Pressable style={styles.backdrop} onPress={onClose} accessibilityLabel="Close" />
      <View style={[styles.sheet, { paddingBottom: bottomInset + 10 }]}>
        <View style={styles.header}>
          <Text style={styles.title}>{title}</Text>
          <Pressable onPress={onClose} accessibilityRole="button" style={styles.close}>
            <Text style={styles.closeText}>Close</Text>
          </Pressable>
        </View>
        {children}
      </View>
    </Modal>
  );
}

interface LogSheetProps {
  log: LogEntry[];
  subtitle: string;
  confirm: boolean;
  onConfirm: (on: boolean) => void;
  onNewGame: () => void;
  bottomInset: number;
  onClose: () => void;
}

export function LogSheet({ log, subtitle, confirm, onConfirm, onNewGame, bottomInset, onClose }: LogSheetProps) {
  const scroll = useRef<ScrollView>(null);
  return (
    <Sheet title="Play-by-play" bottomInset={bottomInset} onClose={onClose}>
      {subtitle ? <Text style={styles.sub}>{subtitle}</Text> : null}
      <ConfirmToggle value={confirm} onChange={onConfirm} />
      <ScrollView
        ref={scroll}
        style={styles.body}
        contentContainerStyle={styles.logBody}
        onContentSizeChange={() => scroll.current?.scrollToEnd({ animated: false })}
      >
        {log.map((e) => (
          <LogLine key={e.seq} e={e} />
        ))}
      </ScrollView>
      <Pressable onPress={onNewGame} accessibilityRole="button" style={styles.newGame}>
        <Text style={styles.newGameText}>New game</Text>
      </Pressable>
    </Sheet>
  );
}

interface HandSheetProps {
  label: string;
  cards: CardInst[];
  bottomInset: number;
  onClose: () => void;
}

export function HandSheet({ label, cards, bottomInset, onClose }: HandSheetProps) {
  return (
    <Sheet title={`${label} · view only`} bottomInset={bottomInset} onClose={onClose}>
      <ScrollView style={styles.body} contentContainerStyle={styles.cardList}>
        {cards.length === 0 ? <Text style={styles.sub}>Empty</Text> : null}
        {cards.map((inst) => {
          const c = DB[inst.id];
          const cost = c.cost ? (c.cost.kind === 'all' ? `all, ${c.cost.min}+` : String(c.cost.value)) : '';
          return (
            <View key={inst.uid} style={styles.cardRow}>
              <Text style={styles.cardName}>
                {c.name.toUpperCase()}
                {cost ? <Text style={styles.cardCost}>{`  ${cost}`}</Text> : null}
              </Text>
              <Text style={styles.cardRules}>{c.rulesText}</Text>
            </View>
          );
        })}
      </ScrollView>
    </Sheet>
  );
}

export function ConfirmToggle({ value, onChange, dark }: { value: boolean; onChange: (on: boolean) => void; dark?: boolean }) {
  return (
    <View style={styles.toggle}>
      <Text style={[styles.toggleText, dark && { color: CG.ink }]}>Confirm plays (tap to select, tap again to play)</Text>
      <Switch value={value} onValueChange={onChange} trackColor={{ true: CG.gold }} />
    </View>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, backgroundColor: 'rgba(0, 0, 0, 0.45)' },
  sheet: {
    maxHeight: '75%',
    backgroundColor: CG.paper,
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    borderTopWidth: 2,
    borderColor: CG.ink,
    paddingHorizontal: 14,
    paddingTop: 12,
    gap: 8,
  },
  header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  title: { fontFamily: FONT_DISPLAY, fontSize: 16, color: CG.ink },
  close: { borderWidth: 2, borderColor: CG.ink, borderRadius: 8, paddingHorizontal: 10, paddingVertical: 4 },
  closeText: { fontSize: 13, fontWeight: '700', color: CG.ink },
  sub: { fontFamily: FONT_MONO, fontSize: 10, color: CG.inkSoft },
  body: { flexGrow: 0 },
  logBody: { gap: 3, paddingBottom: 6 },
  toggle: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 10 },
  toggleText: { flex: 1, fontSize: 12, color: CG.ink },
  newGame: { alignSelf: 'flex-start', borderWidth: 2, borderColor: CG.ink, borderRadius: 8, paddingHorizontal: 10, paddingVertical: 6 },
  newGameText: { fontSize: 13, fontWeight: '700', color: CG.ink },
  cardList: { gap: 10, paddingBottom: 6 },
  cardRow: { gap: 2 },
  cardName: { fontFamily: FONT_DISPLAY, fontSize: 13, color: CG.ink },
  cardCost: { fontFamily: FONT_MONO, fontSize: 11, color: CG.inkSoft },
  cardRules: { fontSize: 12, lineHeight: 16, color: CG.ink },
});
