// What the game screen shows, derived from engine state. Pure, so it's testable
// without rendering.

import { BASE, READS, ord, spot } from './engine';
import type { CardInst, GameState } from './types';

export interface HandSection {
  label: string;
  /** Chip text when collapsed. */
  short: string;
  note: string;
  cards: CardInst[];
}

/**
 * The human's active hands are shown in full; the off-side hand (and the
 * opponent's plays under Film Study) collapse to rows that open a sheet.
 */
export function handSections(G: GameState): { active: HandSection[]; other: HandSection[] } {
  const me = G.players.find((p) => p.human);
  if (!me) return { active: [], other: [] };
  const opp = G.players.find((p) => !p.human)!;
  const d = G.d;
  const onOffense = !d || G.players[d.offIdx] === me;
  if (onOffense) {
    return {
      active: [
        { label: 'Plays', short: 'Plays', note: `${me.hand.plays.length}/7 + base`, cards: me.hand.plays.concat(BASE) },
        { label: 'Specials', short: 'Specials', note: `${me.hand.ospec.length}/5`, cards: me.hand.ospec },
      ],
      other: [{ label: 'Defense specials', short: 'Def', note: '', cards: me.hand.dspec }],
    };
  }
  const other: HandSection[] = [{ label: 'Offense hand', short: 'Off', note: '', cards: me.hand.plays.concat(me.hand.ospec) }];
  if (d.filmStudy) other.unshift({ label: 'Their plays (Film Study)', short: 'Theirs', note: '', cards: opp.hand.plays });
  return {
    active: [
      { label: 'Reads', short: 'Reads', note: 'always in hand', cards: READS },
      { label: 'Specials', short: 'Specials', note: `${me.hand.dspec.length}/7`, cards: me.hand.dspec },
    ],
    other,
  };
}

/** Effects in force this possession or this snap. */
export function activeFlags(G: GameState): string[] {
  const d = G.d, S = G.S, f: string[] = [];
  if (!d) return f;
  if (d.sticky) f.push('Sticky Fingers');
  if (d.hurry) f.push('Hurry-Up');
  if (d.filmStudy) f.push('Film Study');
  if (S) {
    if (S.isKick && S.kick) f.push(`FG ${S.kick.dist} yds`);
    if (S.goalLine) f.push('Goal Line Stand');
    if (S.playAction) f.push('Play Action');
    if (S.hotRoute) f.push('Hot Route');
    if (S.stunt) f.push('Stunt');
    if (S.bracket) f.push('Bracket');
    if (S.spy) f.push('QB Spy');
    if (S.prevent) f.push('Prevent');
    if (S.zoneBlitz) f.push('Zone Blitz');
    if (S.forceLevel) f.push('Two-Minute');
  }
  return f;
}

/** Score bar middle: down and distance (or the game phase), then the spot. */
export function situation(G: GameState): { main: string; sub: string } {
  const d = G.d, S = G.S;
  if (G.over) return { main: 'Final', sub: G.ot ? 'after OT' : '10 possessions' };
  if (d && d.ytg <= 0) return { main: 'Touchdown', sub: d.label };
  if (d) {
    return {
      main: S && S.isTry ? '2-pt try' : `${ord(d.down)} & ${d.dist >= d.ytg ? 'Goal' : d.dist}`,
      sub: `${spot(d.ytg)} · ${d.label.replace('Possession ', 'Poss ')}`,
    };
  }
  return { main: 'Kickoff', sub: '5 possessions each' };
}

/** What the snap row shows for the read and the play: a face, a face-down back, or nothing. */
export function snapCards(G: GameState): { read: CardInst | 'back' | null; play: CardInst | 'back' | null } {
  const S = G.S;
  if (!S || S.isKick) return { read: null, play: null };
  let read: CardInst | 'back' | null = null;
  let play: CardInst | 'back' | null = null;
  if (S.read) {
    const visible = S.revealed || S.defP.human || (S.readShown && S.offP.human);
    read = visible ? READS.find((r) => r.read === S.read)! : 'back';
  }
  if (S.play) play = S.revealed || S.offP.human ? S.play : 'back';
  return { read, play };
}
