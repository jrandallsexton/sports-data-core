import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import type { GameState } from '@/src/utils/cardgame/types';
import { situation } from '@/src/utils/cardgame/view';
import { CG, FONT_DISPLAY, FONT_MONO } from './palette';

// Score bar: names, scores, down and distance. Stamina rides under the team it
// belongs to as chips (LVL and BANK under the offense, DEF under the defense),
// so there's no separate meters row. The thin field strip sits below it.

interface Props {
  G: GameState | null;
  topInset: number;
  onBack: () => void;
}

export function ScoreBar({ G, topInset, onBack }: Props) {
  const d = G?.d ?? null;
  const sit = G ? situation(G) : { main: 'Kickoff', sub: '5 possessions each' };
  const team = (i: number) => {
    const p = G?.players[i];
    return { name: p?.name ?? (i === 0 ? 'You' : 'CPU'), score: p?.score ?? 0, ball: !!d && d.offIdx === i };
  };
  const chips = (i: number) => {
    if (!d) return null;
    if (d.offIdx === i) {
      return (
        <>
          <Chip label="LVL" value={d.off.level} />
          <Chip label="BANK" value={d.off.bank} />
        </>
      );
    }
    return <Chip label="DEF" value={d.def.bank} def />;
  };
  const a = team(0), b = team(1);

  return (
    <View>
      <View style={[styles.bar, { paddingTop: topInset + 6 }]}>
        <Pressable onPress={onBack} hitSlop={10} accessibilityRole="button" accessibilityLabel="Leave the game" style={styles.back}>
          <Text allowFontScaling={false} style={styles.backText}>‹</Text>
        </Pressable>
        <View style={styles.side}>
          <View style={styles.teamRow}>
            {a.ball ? <Text allowFontScaling={false} style={styles.ball}>●</Text> : null}
            <Text allowFontScaling={false} numberOfLines={1} style={styles.name}>{a.name.toUpperCase()}</Text>
            <Text allowFontScaling={false} style={styles.score}>{a.score}</Text>
          </View>
          <View style={styles.chips}>{chips(0)}</View>
        </View>
        <View style={styles.mid}>
          <Text allowFontScaling={false} style={styles.midMain}>{sit.main}</Text>
          <Text allowFontScaling={false} numberOfLines={1} style={styles.midSub}>{sit.sub.toUpperCase()}</Text>
        </View>
        <View style={[styles.side, styles.right]}>
          <View style={styles.teamRow}>
            <Text allowFontScaling={false} style={styles.score}>{b.score}</Text>
            <Text allowFontScaling={false} numberOfLines={1} style={styles.name}>{b.name.toUpperCase()}</Text>
            {b.ball ? <Text allowFontScaling={false} style={styles.ball}>●</Text> : null}
          </View>
          <View style={styles.chips}>{chips(1)}</View>
        </View>
      </View>
      <FieldStrip G={G} />
    </View>
  );
}

function Chip({ label, value, def }: { label: string; value: number; def?: boolean }) {
  return (
    <View style={styles.chip}>
      <Text allowFontScaling={false} style={styles.chipText}>
        {label} <Text style={{ color: def ? CG.defChip : CG.gold }}>{value}</Text>
      </Text>
    </View>
  );
}

/** Line of scrimmage (blue), first-down line (gold), and the ball. */
function FieldStrip({ G }: { G: GameState | null }) {
  const d = G?.d ?? null;
  const pos = d ? Math.min(100, 100 - d.ytg) : 25;
  const fd = d ? Math.min(100, 100 - d.ytg + d.dist) : 35;
  const showFd = !!d && !G?.S?.isTry;
  return (
    <View style={styles.field} accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
      {[1, 2, 3, 4, 5, 6, 7, 8, 9].map((i) => (
        <View key={i} style={[styles.yardLine, { left: `${i * 10}%` }]} />
      ))}
      <View style={[styles.marker, { left: `${pos}%`, backgroundColor: CG.select }]} />
      {showFd ? <View style={[styles.marker, { left: `${fd}%`, backgroundColor: CG.gold }]} /> : null}
      <View style={[styles.football, { left: `${pos}%` }]} />
    </View>
  );
}

const styles = StyleSheet.create({
  bar: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: CG.ink,
    paddingHorizontal: 8,
    paddingBottom: 7,
  },
  back: { width: 18, alignItems: 'center' },
  backText: { color: CG.chalk, fontSize: 26, lineHeight: 28 },
  side: { flex: 1, gap: 4, minWidth: 0 },
  right: { alignItems: 'flex-end' },
  teamRow: { flexDirection: 'row', alignItems: 'baseline', gap: 6, minWidth: 0 },
  name: { fontFamily: FONT_DISPLAY, fontSize: 13, color: CG.chalk, flexShrink: 1 },
  score: { fontFamily: FONT_DISPLAY, fontSize: 24, lineHeight: 26, color: CG.gold, fontVariant: ['tabular-nums'] },
  ball: { color: CG.gold, fontSize: 10 },
  chips: { flexDirection: 'row', gap: 4, minHeight: 17 },
  chip: { borderWidth: 1.5, borderColor: CG.chalkFaint, borderRadius: 4, paddingHorizontal: 5, paddingVertical: 1 },
  chipText: { fontFamily: FONT_MONO, fontSize: 10, color: CG.chalk },
  mid: { alignItems: 'center' },
  midMain: { fontFamily: FONT_DISPLAY, fontSize: 16, color: CG.chalk },
  midSub: { fontFamily: FONT_MONO, fontSize: 9.5, color: CG.chalk, opacity: 0.75 },
  field: { height: 18, backgroundColor: CG.turf, borderBottomWidth: 2, borderBottomColor: CG.ink, overflow: 'hidden' },
  yardLine: { position: 'absolute', top: 0, bottom: 0, width: 1, backgroundColor: 'rgba(251, 248, 240, 0.35)' },
  marker: { position: 'absolute', top: 0, bottom: 0, width: 3, marginLeft: -1 },
  football: {
    position: 'absolute',
    top: 4,
    width: 14,
    height: 9,
    marginLeft: -7,
    borderRadius: 7,
    backgroundColor: CG.ball,
    borderWidth: 1.5,
    borderColor: CG.ink,
  },
});
