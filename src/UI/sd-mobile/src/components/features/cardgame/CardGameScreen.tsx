import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Pressable, StyleSheet, useWindowDimensions, View, type LayoutChangeEvent } from 'react-native';
import { StatusBar } from 'expo-status-bar';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Text } from '@/src/components/ui/AppText';
import { useCardGame, useConfirmPlays } from '@/src/hooks/useCardGame';
import { useMiniGameStats } from '@/src/hooks/useMiniGameStats';
import { DB, GAME_KEY, READS } from '@/src/utils/cardgame/engine';
import type { CardInst } from '@/src/utils/cardgame/types';
import { handSections } from '@/src/utils/cardgame/view';
import type { CardMark } from './GameCard';
import { ConfirmToggle, HandSheet, LogSheet } from './GameSheet';
import { HandArea } from './HandArea';
import { fitHand } from './handLayout';
import { PromptBar, type PromptAction } from './PromptBar';
import { RecordPanel } from './RecordPanel';
import { ScoreBar } from './ScoreBar';
import { SnapRow } from './SnapRow';
import { CG, FONT_DISPLAY } from './palette';

// The football card game (docs/features/card-game/football-card-game.md,
// section 13). One fixed screen, no page scroll: score bar, field strip, snap
// row, prompt bar, then the hand, which gets the rest of the screen.
//
// Playing a card: one tap on a gold (playable) card plays it. Long-press any
// card -- or tap one that isn't playable -- to read its rules in the prompt
// bar. "Confirm plays" makes the first tap select and the second play.

export function CardGameScreen({ onExit }: { onExit: () => void }) {
  const insets = useSafeAreaInsets();
  const { game, error, start } = useCardGame();
  const [confirm, setConfirm] = useConfirmPlays();
  const { stats, summary, hydrated } = useMiniGameStats(GAME_KEY);
  const [sel, setSel] = useState<string | null>(null);
  const [sheet, setSheet] = useState<'log' | number | null>(null);
  const win = useWindowDimensions();
  const [area, setArea] = useState({ width: 0, height: 0 });
  const [promptH, setPromptH] = useState(60);
  // The prompt's height with no card being inspected; it sets the column count.
  const [restingPromptH, setRestingPromptH] = useState(60);

  const G = game?.state ?? null;
  const prompt = game?.prompt ?? null;

  // A new decision clears any card being inspected.
  useEffect(() => setSel(null), [prompt]);

  const legal = useMemo(() => new Set(prompt?.cards.map((c) => c.uid) ?? []), [prompt]);
  const sections = G ? handSections(G) : { active: [], other: [] };

  const find = (uid: string | null): CardInst | null => {
    if (!uid || !G) return null;
    for (const s of sections.active.concat(sections.other)) {
      const i = s.cards.find((c) => c.uid === uid);
      if (i) return i;
    }
    if (G.S?.play?.uid === uid) return G.S.play;
    return READS.find((r) => r.uid === uid) ?? null;
  };
  const selected = G && !G.over ? find(sel) : null;

  const play = useCallback(
    (inst: CardInst) => {
      setSel(null);
      game?.resolve(inst);
    },
    [game],
  );
  const onCardPress = (inst: CardInst) => {
    if (legal.has(inst.uid) && (!confirm || sel === inst.uid)) {
      play(inst);
      return;
    }
    setSel((cur) => (cur === inst.uid ? null : inst.uid));
  };
  const onCardLongPress = (inst: CardInst) => setSel(inst.uid);

  const prompting = !!prompt && prompt.cards.length > 0;
  const markOf = (inst: CardInst): CardMark =>
    sel === inst.uid ? 'selected' : legal.has(inst.uid) ? 'legal' : prompting ? 'dim' : null;

  const onArea = (e: LayoutChangeEvent) => {
    const { width, height } = e.nativeEvent.layout;
    setArea((a) => (a.width === width && a.height === height ? a : { width, height }));
  };
  const onPrompt = (e: LayoutChangeEvent) => {
    const h = Math.round(e.nativeEvent.layout.height);
    setPromptH((cur) => (cur === h ? cur : h));
    if (!sel) setRestingPromptH((cur) => (cur === h ? cur : h));
  };

  // Until the game area is measured, estimate it from the window (score bar and field strip ~80).
  const box = area.width ? area : { width: win.width, height: win.height - insets.top - 80 };
  const counts = sections.active.map((s) => s.cards.length);
  // Columns come from the resting prompt, so inspecting a card (a taller prompt) only shortens
  // the cards instead of jumping the grid from 4 across to 5.
  const resting = fitHand({ width: box.width, height: box.height - restingPromptH - insets.bottom, sections: counts });
  const layout = promptH === restingPromptH
    ? resting
    : fitHand({ width: box.width, height: box.height - promptH - insets.bottom, sections: counts, cols: resting.cols });

  // Prompt bar content.
  let title: string;
  let text: string | undefined;
  let hint: string | undefined;
  const actions: PromptAction[] = [];
  const cpu = G?.players[1].name ?? 'CPU';
  if (error) {
    title = 'Something broke';
    text = error;
    actions.push({ label: 'New game', onPress: start, primary: true });
  } else if (G?.over) {
    const [a, b] = G.players;
    const w = a.score > b.score ? a : b;
    title = a.score === b.score ? `A tie: ${a.score}-${b.score}` : `${w.name} win${w.human ? '' : 's'}: ${a.score}-${b.score}`;
    // The finished game is already queued, so the merged record includes it.
    if (summary.games) text = `Record ${summary.record}${summary.streak ? ` · Streak ${summary.streak}` : ''}`;
    actions.push({ label: 'Play again', onPress: start, primary: true });
  } else if (prompt || selected) {
    title = prompt?.title ?? `${cpu} is thinking…`;
    text = prompt?.text;
    if (selected && legal.has(selected.uid)) {
      actions.push({ label: `Play ${DB[selected.id].name}`, onPress: () => play(selected), primary: true });
    }
    prompt?.buttons.forEach((b) =>
      actions.push({ label: b.label, onPress: () => game?.resolve(b.value), primary: b.primary && !(selected && legal.has(selected.uid)) }),
    );
    if (!selected && prompting && !actions.length) hint = 'Tap a gold card';
  } else {
    title = `${cpu} is thinking…`;
  }

  const me = G?.players.find((p) => p.human);
  const opp = G?.players.find((p) => !p.human);

  return (
    <View style={styles.screen}>
      <StatusBar style="light" />
      <ScoreBar G={G} topInset={insets.top} onBack={onExit} />
      {!G ? (
        <View style={[styles.intro, { paddingBottom: insets.bottom + 20 }]}>
          <Text style={styles.introTitle}>{'FOOTBALL\nCARD GAME'}</Text>
          <Text style={styles.introText}>
            A playtest build. Offense on your possessions, defense on theirs. Tap a gold card to play it. Long-press any card to read its rules.
          </Text>
          {/* Until the cache loads, a player with a record would see "No games yet". */}
          {hydrated ? <RecordPanel stats={stats} summary={summary} /> : null}
          <ConfirmToggle value={confirm} onChange={setConfirm} dark />
          <Pressable onPress={start} accessibilityRole="button" style={styles.kickoff}>
            <Text style={styles.kickoffText}>Kick off</Text>
          </Pressable>
        </View>
      ) : (
        <View style={styles.area} onLayout={onArea} testID="cardgame-area">
          <>
              <SnapRow
                G={G}
                cardW={layout.cardW}
                cardH={layout.cardH}
                markOf={markOf}
                onCardPress={onCardPress}
                onCardLongPress={onCardLongPress}
                onOpenLog={() => setSheet('log')}
              />
              <PromptBar
                title={title}
                selected={selected ? { name: DB[selected.id].name, rules: DB[selected.id].rulesText } : null}
                text={text}
                hint={hint}
                actions={actions}
                onLayout={onPrompt}
              />
              <HandArea
                active={sections.active}
                other={sections.other}
                layout={layout}
                bottomInset={insets.bottom}
                markOf={markOf}
                onCardPress={onCardPress}
                onCardLongPress={onCardLongPress}
                onOpenOther={setSheet}
              />
          </>
        </View>
      )}
      {G && sheet === 'log' ? (
        <LogSheet
          log={G.log}
          subtitle={[
            me ? `Your decks: ${me.decks.plays.draw.length} plays, ${me.decks.ospec.draw.length} offense and ${me.decks.dspec.draw.length} defense specials left.` : '',
            opp ? `${opp.name} holds ${opp.hand.plays.length} plays, ${opp.hand.ospec.length} offense and ${opp.hand.dspec.length} defense specials.` : '',
          ].filter(Boolean).join(' ')}
          confirm={confirm}
          onConfirm={setConfirm}
          onNewGame={() => {
            setSheet(null);
            start();
          }}
          bottomInset={insets.bottom}
          onClose={() => setSheet(null)}
        />
      ) : null}
      {G && typeof sheet === 'number' && sections.other[sheet] ? (
        <HandSheet
          label={sections.other[sheet].label}
          cards={sections.other[sheet].cards}
          bottomInset={insets.bottom}
          onClose={() => setSheet(null)}
        />
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: CG.table },
  area: { flex: 1 },
  intro: { flex: 1, justifyContent: 'center', paddingHorizontal: 20, gap: 14 },
  introTitle: { fontFamily: FONT_DISPLAY, fontSize: 38, lineHeight: 40, color: CG.ink },
  introText: { fontSize: 14, lineHeight: 20, color: CG.ink },
  kickoff: {
    alignSelf: 'flex-start',
    borderWidth: 2,
    borderColor: CG.ink,
    borderRadius: 8,
    paddingHorizontal: 14,
    paddingVertical: 8,
    backgroundColor: CG.chalk,
  },
  kickoffText: { fontSize: 15, fontWeight: '700', color: CG.ink },
});
