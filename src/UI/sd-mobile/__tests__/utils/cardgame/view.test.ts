import { BASE, createGame, READS, seededRng } from '@/src/utils/cardgame/engine';
import type { Drive, GameState, Snap } from '@/src/utils/cardgame/types';
import { handSections, situation, snapCards } from '@/src/utils/cardgame/view';

const drive = (offIdx: number, over: Partial<Drive> = {}): Drive => ({
  offIdx, ytg: 75, down: 1, dist: 10, off: { level: 1, bank: 1 }, def: { bank: 1 },
  sticky: false, hurry: false, prevRead: null, lastSuccess: false, filmStudy: false, label: 'Possession 1 of 10', ...over,
});

function state(): GameState {
  return createGame({ rng: seededRng(3), delayMs: 0 }).state;
}

describe('card game view', () => {
  it('shows plays and specials on offense, reads and specials on defense', () => {
    const G = state();
    G.d = drive(0);
    const off = handSections(G);
    expect(off.active.map((s) => s.label)).toEqual(['Plays', 'Specials']);
    expect(off.active[0].cards.slice(-2)).toEqual(BASE);
    expect(off.other.map((s) => s.label)).toEqual(['Defense specials']);

    G.d = drive(1, { filmStudy: true });
    const def = handSections(G);
    expect(def.active.map((s) => s.label)).toEqual(['Reads', 'Specials']);
    expect(def.active[0].cards).toEqual(READS);
    expect(def.other.map((s) => s.label)).toEqual(['Their plays (Film Study)', 'Offense hand']);
  });

  it('describes the situation', () => {
    const G = state();
    G.d = drive(0, { ytg: 75, down: 2, dist: 6 });
    expect(situation(G)).toEqual({ main: '2nd & 6', sub: 'own 25 · Poss 1 of 10' });
    G.d = drive(0, { ytg: 8, dist: 10 });
    expect(situation(G).main).toBe('1st & Goal');
    G.d = drive(0, { ytg: -4 });
    expect(situation(G).main).toBe('Touchdown');
  });

  it("keeps the CPU's committed cards face down until the reveal", () => {
    const G = state();
    G.d = drive(1);
    const S: Snap = { d: G.d, offP: G.players[1], defP: G.players[0], isTry: false, read: 'pass', play: BASE[0], playCost: 0, R: null, window: 'pre-snap' };
    G.S = S;
    expect(snapCards(G)).toEqual({ read: READS[1], play: 'back' });
    S.revealed = true;
    expect(snapCards(G).play).toBe(BASE[0]);
  });
});
