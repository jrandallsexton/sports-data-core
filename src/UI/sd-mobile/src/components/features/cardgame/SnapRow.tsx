import React from 'react';
import { Pressable, StyleSheet, Text as RNText, View } from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { recentLog } from '@/src/utils/cardgame/engine';
import type { CardInst, GameState, LogEntry } from '@/src/utils/cardgame/types';
import { activeFlags, snapCards } from '@/src/utils/cardgame/view';
import { GameCard, RING, type CardMark } from './GameCard';
import { CG, FONT_DISPLAY, FONT_MONO } from './palette';

// Snap row: the read and play cards (same width as the hand's cards), then the
// current snap's last few log lines. Tapping the feed opens the full log.

interface Props {
  G: GameState;
  cardW: number;
  cardH: number;
  markOf: (inst: CardInst) => CardMark;
  onCardPress: (inst: CardInst) => void;
  onCardLongPress: (inst: CardInst) => void;
  onOpenLog: () => void;
}

export function SnapRow({ G, cardW, cardH, markOf, onCardPress, onCardLongPress, onOpenLog }: Props) {
  const { read, play } = snapCards(G);
  const flags = activeFlags(G);
  const slot = (label: string, c: CardInst | 'back' | null) => (
    <View style={{ width: cardW, gap: 3 }}>
      <RNText allowFontScaling={false} style={styles.slotLabel}>{label.toUpperCase()}</RNText>
      {c === null ? (
        <View style={[styles.hole, { height: cardH - RING * 2, margin: RING }]} />
      ) : c === 'back' ? (
        <View style={[styles.back, { height: cardH - RING * 2, margin: RING }]}>
          <RNText allowFontScaling={false} style={styles.backTag}>{label.toUpperCase()}</RNText>
        </View>
      ) : (
        <GameCard inst={c} width={cardW} height={cardH} mark={markOf(c)} onPress={onCardPress} onLongPress={onCardLongPress} />
      )}
    </View>
  );

  return (
    <View style={styles.row}>
      {slot('Read', read)}
      {slot('Play', play)}
      <Pressable style={styles.feed} onPress={onOpenLog} accessibilityRole="button" accessibilityLabel="Open the full play-by-play">
        {flags.length ? (
          <View style={styles.flags}>
            {flags.map((f) => (
              <View key={f} style={styles.flag}>
                <RNText allowFontScaling={false} style={styles.flagText}>{f.toUpperCase()}</RNText>
              </View>
            ))}
          </View>
        ) : null}
        {recentLog(G).slice(-4).map((e) => (
          <LogLine key={e.seq} e={e} />
        ))}
        <RNText allowFontScaling={false} style={styles.more}>FULL LOG ›</RNText>
      </Pressable>
    </View>
  );
}

const EDGE: Record<string, string> = { off: '#4b99d7', def: '#f4514f', big: CG.gold, 'def big': '#f4514f' };

export function LogLine({ e }: { e: LogEntry }) {
  if (e.cls === 'head') {
    return <RNText allowFontScaling={false} style={styles.head}>{e.text.toUpperCase()}</RNText>;
  }
  return (
    <View style={[styles.line, { borderLeftColor: EDGE[e.cls] ?? 'transparent' }]}>
      <Text
        style={[
          styles.lineText,
          e.cls === 'sys' && styles.sys,
          (e.cls === 'big' || e.cls === 'def big') && styles.big,
        ]}
      >
        {e.text}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    gap: 8,
    paddingHorizontal: 10 - RING,
    paddingVertical: 8,
    backgroundColor: CG.paper,
    borderBottomWidth: 2,
    borderBottomColor: CG.ink,
  },
  slotLabel: { fontFamily: FONT_MONO, fontSize: 8.5, color: CG.inkSoft, textAlign: 'center', height: 11 },
  hole: { borderWidth: 1.5, borderStyle: 'dashed', borderColor: CG.inkSoft, borderRadius: 7 },
  back: {
    borderWidth: 2,
    borderColor: CG.ink,
    borderRadius: 7,
    backgroundColor: '#34302a',
    alignItems: 'center',
    justifyContent: 'center',
  },
  backTag: {
    fontFamily: FONT_DISPLAY,
    fontSize: 9,
    color: CG.ink,
    backgroundColor: CG.gold,
    paddingHorizontal: 5,
    paddingVertical: 2,
    borderRadius: 4,
    overflow: 'hidden',
  },
  feed: { flex: 1, minWidth: 0, gap: 2 },
  flags: { flexDirection: 'row', flexWrap: 'wrap', gap: 3, marginBottom: 2 },
  flag: { backgroundColor: CG.ink, borderRadius: 4, paddingHorizontal: 5, paddingVertical: 1 },
  flagText: { fontFamily: FONT_MONO, fontSize: 8.5, color: CG.gold },
  line: { borderLeftWidth: 3, paddingLeft: 6 },
  lineText: { fontSize: 12, lineHeight: 16, color: CG.ink },
  sys: { color: CG.inkSoft, fontSize: 11 },
  big: { fontWeight: '800' },
  head: { fontFamily: FONT_MONO, fontSize: 9.5, color: CG.inkSoft },
  more: { marginTop: 'auto', fontFamily: FONT_MONO, fontSize: 9, color: CG.inkSoft },
});
