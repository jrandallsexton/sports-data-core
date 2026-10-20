// Football card game engine: a TypeScript port of the prototype's turn loop
// (docs/features/card-game/play.html). Pure -- no React, no I/O. The human's
// decisions come in through the ask() seam: the engine publishes a Prompt and
// awaits resolve(), so a screen or a test supplies the choices.

import cardsJson from './cards.json';
import type {
  CardDef,
  CardInst,
  CommitChoice,
  Drive,
  GameState,
  HandKey,
  LogClass,
  LogEntry,
  Player,
  Prompt,
  ReadKind,
  Side,
  Snap,
  SnapResult,
} from './types';

export const DB: Record<string, CardDef> = Object.fromEntries(
  (cardsJson.cards as unknown as CardDef[]).map((c) => [c.id, c]),
);

export const HAND_MAX: Record<HandKey, number> = { plays: 7, ospec: 5, dspec: 7 };
const DECK_NAME: Record<HandKey, string> = { plays: 'play', ospec: 'offense specials', dspec: 'defense specials' };
export const WIN_LABEL: Record<string, string> = { commit: 'Commit', drive: 'Drive', 'pre-snap': 'Pre-snap', 'post-reveal': 'Post-reveal', 'post-snap': 'Post-snap' };
export const WIN_SHORT: Record<string, string> = { drive: 'Drive', 'pre-snap': 'Pre', 'post-reveal': 'Reveal', 'post-snap': 'Post' };
// Counters that are only ever played in answer to another card.
const RESPONSE_ONLY = new Set(['161', '162', '165', '166', '269']);
export const BASE: CardInst[] = [{ uid: 'base-141', id: '141', base: true }, { uid: 'base-142', id: '142', base: true }];
export const READS: CardInst[] = [
  { uid: 'read-201', id: '201', read: 'run' },
  { uid: 'read-202', id: '202', read: 'pass' },
  { uid: 'read-203', id: '203', read: 'blitz' },
];

export const ord = (n: number) => ['1st', '2nd', '3rd', '4th'][n - 1] || `${n}th`;
export const spot = (ytg: number) => (ytg > 50 ? `own ${100 - ytg}` : ytg === 50 ? 'midfield' : `opp ${ytg}`);

/** Fixed cost of a card, 0 for "spend all" cards. Play cards are always fixed. */
export const fixedCost = (id: string) => {
  const c = DB[id].cost;
  return c && c.kind === 'fixed' ? c.value : 0;
};

/** Deterministic PRNG (mulberry32) so tests can replay a game. */
export function seededRng(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** The current snap's log lines; until it has any, the previous snap's. */
export function recentLog(G: GameState): LogEntry[] {
  const rows = G.log.filter((e) => e.seq >= G.playAt);
  return rows.length ? rows : G.log.filter((e) => e.seq >= G.prevPlayAt);
}

export interface GameOptions {
  /** Defaults to Math.random. */
  rng?: () => number;
  /** CPU "thinking" pause. 0 for tests. */
  delayMs?: number;
  /** false = CPU vs CPU (the smoke test). */
  human?: boolean;
  /** Called after every state change. */
  onChange?: () => void;
}

export interface CardGame {
  readonly state: GameState;
  /** The decision the human owes, if any. */
  readonly prompt: Prompt | null;
  /** Answer the current prompt with a card from prompt.cards or a button's value. */
  resolve(value: unknown): void;
  /** Runs the whole game; resolves when it's over (or rejects with Abandoned). */
  run(): Promise<void>;
  /** Stops a game in progress (screen closed, new game). */
  abandon(): void;
}

export class Abandoned extends Error {
  constructor() {
    super('Game abandoned');
  }
}

type Effect = {
  can: (S: Snap, side: Side) => boolean | null | undefined | CardInst | SnapResult;
  apply: (S: Snap, side: Side) => void | Promise<void>;
};

export function createGame(opts: GameOptions = {}): CardGame {
  const rnd = opts.rng ?? Math.random;
  const delayMs = opts.delayMs ?? 550;
  const human = opts.human ?? true;
  const notify = () => {
    if (!abandoned) opts.onChange?.();
  };
  let abandoned = false;
  let uidSeq = 0;
  let pending: (Prompt & { resolve: (v: unknown) => void }) | null = null;
  let rejectPending: ((e: Error) => void) | null = null;

  const G: GameState = {
    players: [newPlayer(human ? 'You' : 'CPU A', human), newPlayer(human ? 'CPU' : 'CPU B', false)],
    log: [], seq: 0, playAt: 1, prevPlayAt: 1, first: 0, poss: 0, ot: 0, over: false, d: null, S: null,
  };
  G.first = rnd() < 0.5 ? 0 : 1;

  const sleep = (ms: number) => new Promise<void>((r) => setTimeout(r, ms));
  const pause = async () => {
    if (delayMs) await sleep(delayMs);
    if (abandoned) throw new Abandoned();
  };

  function shuffle<T>(a: T[]): T[] {
    for (let i = a.length - 1; i > 0; i--) {
      const j = Math.floor(rnd() * (i + 1));
      [a[i], a[j]] = [a[j], a[i]];
    }
    return a;
  }
  function weighted<T>(opts2: [T, number][]): T {
    const t = opts2.reduce((s, o) => s + o[1], 0);
    let r = rnd() * t;
    for (const [v, w] of opts2) {
      r -= w;
      if (r <= 0) return v;
    }
    return opts2[opts2.length - 1][0];
  }

  /* ---------- decks & hands ---------- */
  function buildDeck(name: string): CardInst[] {
    const d: CardInst[] = [];
    for (const c of Object.values(DB)) {
      const n = c.copies[name] || 0;
      for (let i = 0; i < n; i++) d.push({ uid: `c${++uidSeq}`, id: c.id });
    }
    return shuffle(d);
  }
  function newPlayer(name: string, isHuman: boolean): Player {
    return {
      name, human: isHuman, score: 0,
      decks: {
        plays: { draw: buildDeck('offense-plays'), discard: [] },
        ospec: { draw: buildDeck('offense-specials'), discard: [] },
        dspec: { draw: buildDeck('defense-specials'), discard: [] },
      },
      hand: { plays: [], ospec: [], dspec: [] },
    };
  }
  function draw(p: Player, k: HandKey, n = 1) {
    for (let i = 0; i < n; i++) {
      if (p.hand[k].length >= HAND_MAX[k]) return;
      const d = p.decks[k];
      if (!d.draw.length) {
        if (!d.discard.length) return;
        d.draw = shuffle(d.discard);
        d.discard = [];
        log(`${p.name}: ${DECK_NAME[k]} deck reshuffled.`, 'sys');
      }
      const inst = d.draw.pop()!;
      inst.k = k;
      p.hand[k].push(inst);
    }
  }
  function drawUpTo(p: Player, k: HandKey, n: number) {
    let guard = 20;
    while (p.hand[k].length < n && guard--) {
      const b = p.hand[k].length;
      draw(p, k, 1);
      if (p.hand[k].length === b) break;
    }
  }
  // Base plays and reads never leave the hand.
  function takeFromHand(p: Player, inst: CardInst) {
    if (inst.base || inst.read || !inst.k) return;
    const h = p.hand[inst.k];
    const i = h.findIndex((x) => x.uid === inst.uid);
    if (i >= 0) h.splice(i, 1);
  }
  function toDiscard(p: Player, inst: CardInst) {
    if (inst.base || inst.read || !inst.k) return;
    p.decks[inst.k].discard.push(inst);
  }
  function toHand(p: Player, inst: CardInst) {
    if (inst.base || inst.read || !inst.k) return;
    p.hand[inst.k].push(inst);
  }

  /* ---------- state helpers ---------- */
  const P = (S: Snap, side: Side) => (side === 'off' ? S.offP : S.defP);
  const handKey = (side: Side): HandKey => (side === 'off' ? 'ospec' : 'dspec');
  const bankOf = (S: Snap, side: Side) => (side === 'off' ? S.d.off : S.d.def);
  const other = (side: Side): Side => (side === 'off' ? 'def' : 'off');
  const playInfo = (S: Snap) => {
    const c = DB[S.play!.id];
    return { id: c.id, type: c.play!.type, yards: c.play!.yards || 0, readYards: c.play!.readYards || 0 };
  };
  function costOf(S: Snap, side: Side, inst: CardInst) {
    const c = DB[inst.id].cost;
    if (!c) return 0;
    return c.kind === 'all' ? bankOf(S, side).bank : c.value;
  }
  function affordable(S: Snap, side: Side, inst: CardInst) {
    const c = DB[inst.id].cost;
    if (!c) return true;
    const bank = bankOf(S, side).bank;
    return c.kind === 'all' ? bank >= c.min : bank >= c.value;
  }
  function log(text: string, cls: LogClass = '') {
    G.log.push({ text, cls, seq: ++G.seq });
    if (G.log.length > 400) G.log.shift();
    notify();
  }
  // A snap's log starts at the drive header if one was just written, else at the next line.
  function markPlay() {
    const last = G.log[G.log.length - 1];
    const at = last && last.cls === 'head' ? last.seq : G.seq + 1;
    if (at === G.playAt) return;
    G.prevPlayAt = G.playAt;
    G.playAt = at;
  }

  /* ---------- card effects (window cards) ---------- */
  const E: Record<string, Effect> = {
    '151': { can: (S) => !S.d.sticky, apply: (S) => { S.d.sticky = true; log('Sticky Fingers: no interception cards on 6+ yards to go this possession.', 'off'); } },
    '152': { can: (S) => !S.isTry && !S.isKick && S.d.down === 4 && S.d.dist <= 1 && S.d.ytg > 50,
      apply: (S) => { S.abort = { type: 'td', text: '4TH AND OMG! Touchdown!' }; } },
    '153': { can: (S) => !S.isKick && S.play, apply: async (S) => { log(`Hard Count: the defense shows ${S.read!.toUpperCase()}.`, 'off'); S.readShown = true; await swapPlay(S, 'any'); } },
    '154': { can: (S) => S.play && !S.play.isSpecialPlay, apply: async (S) => { await swapPlay(S, 'same'); } },
    '155': { can: (S) => S.play && playInfo(S).type === 'pass' && S.play.id !== '159', apply: (S) => { S.playAction = true; } },
    '156': { can: (S) => !S.d.hurry, apply: (S) => { S.d.hurry = true; log('Hurry-Up: after a successful play, the defense must repeat its read.', 'off'); } },
    '157': { can: (S) => !!S.R, apply: (S) => { S.forceLevel = true; } },
    '158': { can: (S) => S.R && S.R.stuffed && !S.R.sacked && playInfo(S).type === 'run' && playInfo(S).yards - 3 > S.R.yards,
      apply: (S) => { S.R!.yards = playInfo(S).yards - 3; log(`Broken Tackle: gains ${S.R!.yards} instead.`, 'off'); } },
    '160': { can: (S) => !S.isTry && !S.isKick && S.d.down === 4 && S.d.dist <= 5, apply: (S) => { S.abort = { type: 'firstdown', text: 'Fake punt! First down.' }; } },
    '164': { can: (S) => S.play && playInfo(S).type === 'pass' && S.read === 'blitz', apply: (S) => { S.hotRoute = true; } },
    '167': { can: (S) => S.isKick, apply: (S) => { S.kick!.bigLeg = true; } },
    '168': { can: () => true, apply: (S) => { S.d.off.bank = Math.min(6, S.d.off.bank + 2); } },
    '169': { can: (S) => S.isTry, apply: (S) => { S.abort = { type: 'good', text: 'Swinging Gate! Two points.' }; } },
    '251': { can: (S) => !S.isTry && !S.isKick, apply: (S) => {
      const d = S.d;
      const back = Math.min(5, 99 - d.ytg);
      d.ytg += back; d.dist += back; d.off.level = Math.max(1, d.off.level - 1); S.gotchya = true;
      log(`Gotchya! False start, ${back} yards back, offense level down to ${d.off.level}. Replay the down.`, 'def');
    } },
    '252': { can: (S) => S.R && S.R.stuffed && !S.R.sacked && !S.isTry && S.read === 'blitz' && playInfo(S).type === 'pass' && !S.zoneBlitz && !S.hotRoute,
      apply: (S) => { const R = S.R!; R.yards = -7; R.sacked = true; R.levelReset = true; S.d.def.bank = Math.min(5, S.d.def.bank + 1); log('SACK! Loss of 7, offense level resets to 1.', 'def big'); } },
    '253': { can: (S) => S.R && !S.R.turnover && !S.R.stuffed && S.R.yards > 0 && playInfo(S).type === 'run',
      apply: (S) => { S.R!.turnover = 'fumble'; log('PUNCHBALL! Fumble, the defense recovers.', 'def big'); } },
    '254': { can: (S) => S.R && !S.R.turnover && !S.R.stuffed && playInfo(S).type === 'pass' && !(S.d.sticky && S.d.dist > 5),
      apply: (S) => { S.R!.turnover = 'int'; S.R!.hijacked = true; log('HIJACKED! Intercepted.', 'def big'); } },
    '255': { can: (S) => S.read === 'blitz', apply: (S) => { S.stunt = true; } },
    '256': { can: (S) => S.play && playInfo(S).type === 'pass', apply: (S) => { S.bracket = true; } },
    '257': { can: (S) => S.d.off.level > 1, apply: (S) => { S.d.off.level = Math.max(1, S.d.off.level - 1); log(`Crowd Noise: offense level down to ${S.d.off.level}.`, 'def'); } },
    '258': { can: (S) => S.R && S.R.hijacked && !S.isTry && !(S.d.sticky && S.d.dist > 5), apply: (S) => { S.R!.defScore = 7; log('PICK SIX! The defense scores.', 'def big'); } },
    '259': { can: (S) => S.R && S.R.sacked && !S.R.turnover, apply: (S) => { S.R!.turnover = 'fumble'; log('STRIP SACK! Ball is out, defense recovers.', 'def big'); } },
    '260': { can: (S) => S.d.ytg <= 5 && !S.isKick, apply: (S) => { S.goalLine = true; log('Goal Line Stand: a Run read also stuffs Passes this snap.', 'def'); } },
    '261': { can: (S) => S.isKick, apply: (S) => { S.kick!.iced = true; } },
    '262': { can: (S) => !S.d.filmStudy, apply: (S) => { S.d.filmStudy = true; log("Film Study: the defense sees the offense's play hand this drive.", 'def'); } },
    '263': { can: (S) => S.read === 'blitz', apply: (S) => { S.zoneBlitz = true; } },
    '264': { can: (S) => S.play && playInfo(S).type === 'run', apply: (S) => { S.spy = true; } },
    '265': { can: (S) => S.play && playInfo(S).type === 'pass', apply: (S) => { S.prevent = true; } },
    '266': { can: (S) => S.R && !S.R.turnover && S.R.yards >= 10 && !S.isTry,
      apply: (S) => { const R = S.R!; R.yards = -10; R.stuffed = true; R.replayDown = true; log('Holding! The gain comes back, 10-yard penalty, replay the down.', 'def'); } },
    '267': { can: (S) => S.isKick, apply: (S) => { S.kick!.blocked = true; } },
    '268': { can: (S) => S.isKick && S.kick!.dist >= 41, apply: (S) => { S.kick!.blocked = true; S.kick!.six = true; } },
    '270': { can: (S) => S.d.hurry, apply: (S) => { S.d.hurry = false; log('Timeout: Hurry-Up is over.', 'def'); } },
    '271': { can: () => true, apply: (S) => { S.d.def.bank = Math.min(5, S.d.def.bank + 2); } },
    '301': { can: (S) => !!S.R, apply: (S) => { S.R!.negated = true; log('Flag on the play: result negated, replay the down.', 'sys'); } },
  };
  // Extra effects when a counter cancels a card.
  const RESP: Record<string, (S: Snap) => void> = {
    '162': (S) => { const R = S.R; if (R) { R.bonus = 15; R.autoFirst = true; R.turnover = null; R.hijacked = false; R.defScore = 0; } log('Pass interference: +15 and a first down.', 'off'); },
    '166': (S) => { const R = S.R; if (R) { R.yards = 0; R.sacked = false; R.levelReset = false; R.turnover = null; R.bonus = 15; R.autoFirst = true; } log('Roughing the passer: +15 and a first down.', 'off'); },
    '161': (S) => { if (S.R) S.R.turnover = null; },
    '301': (S) => { if (S.R) S.R.negated = true; },
  };

  /* ---------- CPU ---------- */
  function aiRead(S: Snap): ReadKind {
    const d = S.d;
    if (d.hurry && d.lastSuccess && d.prevRead) return d.prevRead;
    let w: Record<ReadKind, number> = d.dist <= 3 ? { run: 0.55, pass: 0.3, blitz: 0.15 } : d.dist >= 8 ? { run: 0.2, pass: 0.5, blitz: 0.3 } : { run: 0.4, pass: 0.4, blitz: 0.2 };
    if (S.isTry) w = { run: 0.45, pass: 0.4, blitz: 0.15 };
    if (d.filmStudy) {
      const hand = S.offP.hand.plays.filter((i) => fixedCost(i.id) <= d.off.bank);
      const runs = hand.filter((i) => DB[i.id].play!.type === 'run').length;
      const passes = hand.length - runs;
      if (runs > passes) w.run += 0.3;
      else if (passes > runs) { w.pass += 0.2; w.blitz += 0.1; }
    }
    return weighted(Object.entries(w) as [ReadKind, number][]);
  }
  function playOptions(S: Snap): CardInst[] {
    const bank = S.d.off.bank;
    const p = S.offP;
    const opts = p.hand.plays.filter((i) => fixedCost(i.id) <= bank).concat(BASE);
    if (!S.isTry) for (const i of p.hand.ospec) if (DB[i.id].window === 'commit' && affordable(S, 'off', i)) opts.push(i);
    return opts;
  }
  const kickRange = (boost: boolean) => 40 + (boost ? 10 : 0);
  function aiCommit(S: Snap): CommitChoice {
    const d = S.d, bank = d.off.bank, kd = d.ytg + 17;
    if (!S.isTry && d.down === 4) {
      const omg = S.offP.hand.ospec.some((i) => i.id === '152') && bank >= 3 && d.dist <= 1 && d.ytg > 50;
      const fake = S.offP.hand.ospec.some((i) => i.id === '160') && bank >= 2 && d.dist <= 5;
      if (!omg && !fake) {
        if (kd <= 40) return { kind: 'kick', boost: false };
        if (kd <= 50 && bank >= 2) return { kind: 'kick', boost: true };
        const bigLeg = S.offP.hand.ospec.some((i) => i.id === '167');
        if (kd <= 50 && bigLeg && bank >= 1) return { kind: 'kick', boost: false };
      }
    }
    let opts = playOptions(S).filter((i) => !(i.id === '159' && !(d.down >= 3 && d.ytg > 30 && rnd() < 0.5)) && !(i.id === '163' && rnd() < 0.5));
    if (!opts.length) opts = BASE.slice();
    if (rnd() < 0.22) return { kind: 'play', inst: opts[Math.floor(rnd() * opts.length)] };
    const yards = (i: CardInst) => (DB[i.id].play ? DB[i.id].play!.yards ?? 30 : 0);
    const cost = (i: CardInst) => costOf(S, 'off', i);
    const reach = opts.filter((i) => yards(i) >= d.dist).sort((a, b) => cost(a) - cost(b) || yards(a) - yards(b));
    if (reach.length && (d.down >= 2 || rnd() < 0.6)) return { kind: 'play', inst: reach[0] };
    opts.sort((a, b) => yards(b) - yards(a));
    return { kind: 'play', inst: opts[0] };
  }
  function aiWants(S: Snap, side: Side, inst: CardInst): number {
    const id = inst.id, d = S.d, R = S.R;
    const info = S.play ? playInfo(S) : null;
    switch (id) {
      case '151': return info && info.type === 'pass' && d.dist > 5 ? 0.6 : 0.1;
      case '152': case '160': case '169': return 1;
      case '153': return 0.35;
      case '154': return info && !S.isTry && S.read !== (info.type === 'run' ? 'run' : 'pass') && S.offP.hand.plays.some((i) => DB[i.id].play!.type === info.type && (DB[i.id].play!.yards ?? 0) > info.yards && fixedCost(i.id) - costOf(S, 'off', S.play!) <= d.off.bank) ? 0.9 : 0;
      case '155': case '164': return 1;
      case '156': return d.lastSuccess ? 0.4 : 0.1;
      case '157': return R && R.stuffed && !R.sacked ? 0.7 : 0;
      case '158': return 1;
      case '167': return S.kick && S.kick.dist > kickRange(S.kick.boost) ? 1 : 0;
      case '168': return d.off.bank <= 3 ? 0.5 : 0.1;
      case '251': return 0.2;
      case '252': case '258': case '259': case '268': return 1;
      case '253': case '254': return R && (R.yards >= 6 || d.ytg - R.yards < 15) ? 0.75 : 0.35;
      case '255': case '263': return info && info.type === 'run' ? 1 : 0;
      case '256': return R === null && info && S.read === 'run' && info.yards >= 8 ? 0.9 : 0.1;
      case '257': return d.off.level >= 3 ? 0.6 : 0.15;
      // Pre-snap: the human sees this play before the reveal, so it must not depend on the hidden read.
      case '260': return 0.5;
      case '261': return S.kick && S.kick.dist > 38 ? 0.9 : 0.3;
      case '262': return 0.3;
      case '264': return info && S.read !== 'run' && info.yards >= 7 ? 0.9 : 0.1;
      case '265': return info && (['159', '163'].includes(info.id) || (S.read === 'run' && info.yards > 12)) ? 1 : 0.05;
      case '266': return 1;
      case '267': return 0.9;
      case '270': return 0.7;
      case '271': return d.def.bank <= 2 ? 0.5 : 0.1;
      case '301': return side === 'def' ? (R && R.yards >= 15 ? 0.6 : 0) : (R && (R.turnover || R.sacked) ? 0.8 : 0);
      default: return 0;
    }
  }

  /* ---------- human prompts ---------- */
  function ask<T = unknown>(req: Prompt): Promise<T> {
    if (abandoned) return Promise.reject(new Abandoned());
    return new Promise<T>((res, rej) => {
      rejectPending = rej;
      pending = { ...req, resolve: (v) => { pending = null; rejectPending = null; res(v as T); notify(); } };
      notify();
    });
  }

  /* ---------- choosing ---------- */
  function legalWindowCards(S: Snap, side: Side): CardInst[] {
    const p = P(S, side);
    return p.hand[handKey(side)].filter((i) => {
      const c = DB[i.id];
      if (c.window !== S.window || RESPONSE_ONLY.has(i.id)) return false;
      const e = E[i.id];
      if (!e) return false;
      return !!e.can(S, side) && affordable(S, side, i);
    });
  }
  async function chooseWindowCard(S: Snap, side: Side, follow = false): Promise<CardInst | null> {
    let legal = legalWindowCards(S, side);
    // Pick Six and Strip Sack only follow the card they finish.
    if (follow) legal = legal.filter((i) => i.id === '258' || i.id === '259');
    else legal = legal.filter((i) => i.id !== '258' && i.id !== '259');
    if (!legal.length) return null;
    const p = P(S, side);
    if (p.human) {
      return ask<CardInst | null>({
        kind: 'window', title: follow ? 'Follow up?' : `${WIN_LABEL[S.window!]} window`,
        text: side === 'off' ? 'Play an offense card, or pass.' : 'Play a defense card, or pass.', cards: legal, buttons: [{ label: 'Pass', value: null }],
      });
    }
    await pause();
    for (const i of shuffle(legal.slice())) if (rnd() < aiWants(S, side, i)) return i;
    return null;
  }
  async function chooseResponse(S: Snap, side: Side, played: CardInst): Promise<CardInst | null> {
    const p = P(S, side);
    const legal = p.hand[handKey(side)].filter((i) => {
      const c = DB[i.id];
      const names = c.counters.includes(played.id);
      const flag = i.id === '301' && S.window === 'post-snap';
      return (names || flag) && affordable(S, side, i);
    });
    if (!legal.length) return null;
    if (p.human) {
      return ask<CardInst | null>({ kind: 'response', title: `${P(S, other(side)).name} played ${DB[played.id].name}`, text: 'Answer it with a counter?', cards: legal, buttons: [{ label: 'No response', value: null }] });
    }
    await pause();
    const named = legal.find((i) => i.id !== '301');
    if (named) return named;
    return rnd() < aiWants(S, side, legal[0]) && ['253', '254', '252', '259', '258', '266'].includes(played.id) ? legal[0] : null;
  }
  async function playCard(S: Snap, side: Side, inst: CardInst) {
    const p = P(S, side), c = DB[inst.id];
    const cost = costOf(S, side, inst);
    bankOf(S, side).bank -= cost;
    takeFromHand(p, inst);
    toDiscard(p, inst);
    log(`${p.name} ${p.human ? 'play' : 'plays'} ${c.name}${cost ? ` (${cost})` : ''}.`, side);
    const resp = await chooseResponse(S, other(side), inst);
    if (resp) {
      const q = P(S, other(side)), rc = DB[resp.id];
      bankOf(S, other(side)).bank -= costOf(S, other(side), resp);
      takeFromHand(q, resp);
      toDiscard(q, resp);
      log(`${q.name} ${q.human ? 'answer' : 'answers'} with ${rc.name}: ${c.name} is canceled.`, other(side));
      // A flag negates the whole snap rather than the card, so the card still applies first.
      // A flagged card is canceled like any other: its effect (e.g. Sack's +1 bank) never applies.
      if (resp.id === '301') { RESP['301'](S); return; }
      RESP[resp.id]?.(S);
      return;
    }
    await E[inst.id].apply(S, side);
    notify();
  }
  async function runWindow(S: Snap) {
    for (const side of ['off', 'def'] as Side[]) {
      if (S.abort || S.gotchya) return;
      const inst = await chooseWindowCard(S, side);
      if (inst) await playCard(S, side, inst);
      if (side === 'def' && S.window === 'post-snap') {
        for (let k = 0; k < 2; k++) {
          const f = await chooseWindowCard(S, 'def', true);
          if (!f) break;
          await playCard(S, 'def', f);
        }
      }
    }
  }
  async function swapPlay(S: Snap, mode: 'any' | 'same') {
    const cur = S.play!, curCost = S.playCost, bank = S.d.off.bank + curCost;
    const curType = playInfo(S).type;
    const opts = S.offP.hand.plays.concat(BASE).filter((i) => i.uid !== cur.uid && fixedCost(i.id) <= bank && (mode === 'any' || DB[i.id].play!.type === curType));
    if (!opts.length) return;
    let pick: CardInst | null = null;
    if (S.offP.human) {
      pick = await ask<CardInst | null>({ kind: 'swap', title: mode === 'any' ? 'Hard Count: swap your play?' : 'Audible: swap to the same type?', text: `The defense read ${S.read!.toUpperCase()}.`, cards: opts, buttons: [{ label: 'Keep my play', value: null }] });
    } else {
      await pause();
      const readType = S.read === 'run' ? 'run' : 'pass';
      const y = (i: CardInst) => DB[i.id].play!.yards ?? 0;
      const good = opts.filter((i) => mode === 'same' || DB[i.id].play!.type !== readType || (i.id === '116' && S.read === 'blitz')).sort((a, b) => y(b) - y(a));
      const curGood = curType !== readType;
      if (good.length && (!curGood || y(good[0]) > playInfo(S).yards)) pick = good[0];
    }
    if (!pick) return;
    S.d.off.bank += curCost;
    if (!cur.base) toHand(S.offP, cur);
    takeFromHand(S.offP, pick);
    S.play = pick;
    S.playCost = fixedCost(pick.id);
    S.d.off.bank -= S.playCost;
    log(`${S.offP.name} switch${S.offP.human ? '' : 'es'} to ${DB[pick.id].name}.`, 'off');
  }

  /* ---------- commits ---------- */
  async function commitRead(S: Snap): Promise<ReadKind> {
    const d = S.d, p = S.defP;
    const locked = d.hurry && d.lastSuccess && d.prevRead;
    if (p.human) {
      const cards = READS.filter((r) => !locked || r.read === d.prevRead);
      const text = locked ? `Hurry-Up: you must repeat ${d.prevRead!.toUpperCase()}.` : S.isTry ? '2-point try from the 3.' : `${ord(d.down)} & ${d.dist >= d.ytg ? 'Goal' : d.dist}, ${spot(d.ytg)}.`;
      const v = await ask<CardInst>({ kind: 'read', title: 'Defense: commit your read', text, cards, buttons: [] });
      return v.read!;
    }
    await pause();
    return aiRead(S);
  }
  async function commitPlay(S: Snap): Promise<CommitChoice> {
    const d = S.d, p = S.offP;
    if (p.human) {
      const opts = playOptions(S);
      const kd = d.ytg + 17;
      const buttons: Prompt['buttons'] = [];
      // No Huddle (D26): a 0-cost base play is always available, so burning a down to draw
      // a card is never worth it.
      if (!S.isTry) {
        if (kd <= 40) buttons.push({ label: `Kick ${kd}-yd FG`, value: { kind: 'kick', boost: false } });
        else if (kd <= 60) buttons.push({ label: `Kick ${kd}-yd FG (needs range)`, value: { kind: 'kick', boost: false } });
        if (kd > 40 && kd <= 60 && d.off.bank >= 2) buttons.push({ label: `Kick ${kd}-yd FG +10 range (2 stamina)`, value: { kind: 'kick', boost: true } });
      }
      const v = await ask<CardInst | CommitChoice>({ kind: 'commit', title: S.isTry ? '2-point try: commit a play' : 'Offense: commit a play', text: `Bank ${d.off.bank}. The defense has committed its read.`, cards: opts, buttons });
      if ('uid' in v) return { kind: 'play', inst: v };
      return v;
    }
    await pause();
    return aiCommit(S);
  }

  /* ---------- snap ---------- */
  type DriveEnd = { type: 'td' | 'fg' | 'missed' | 'turnover' | 'downs' | 'safety' | 'good' | 'nogood'; text?: string } | null;

  async function playSnap(d: Drive, isTry = false): Promise<DriveEnd> {
    const S: Snap = { d, offP: G.players[d.offIdx], defP: G.players[1 - d.offIdx], isTry, read: null, play: null, playCost: 0, R: null, window: null };
    markPlay();
    G.S = S;
    notify();
    if (!isTry) { S.window = 'drive'; await runWindow(S); }
    for (let guard = 0; guard < 6; guard++) {
      S.window = 'commit'; S.gotchya = false; S.read = null; S.play = null;
      notify();
      S.read = await commitRead(S);
      const c = await commitPlay(S);
      if (c.kind === 'kick') return attemptFG(S, c.boost);
      S.play = c.inst;
      S.playCost = costOf(S, 'off', c.inst);
      d.off.bank -= S.playCost;
      if (!c.inst.base) takeFromHand(S.offP, c.inst);
      S.play.isSpecialPlay = DB[c.inst.id].category !== 'play';
      notify();
      S.window = 'pre-snap'; await runWindow(S);
      if (S.abort) return finishAbort(S);
      if (S.gotchya) { d.off.bank += S.playCost; if (!S.play.base) toHand(S.offP, S.play); S.play = null; continue; }
      break;
    }
    S.revealed = true;
    const info = playInfo(S);
    log(`${S.defP.name} read ${S.read!.toUpperCase()}. ${S.offP.name} ran ${DB[S.play!.id].name} (${info.type}${DB[S.play!.id].play!.yards ? `, ${info.yards}` : ''}).`, 'sys');
    await pause();
    S.window = 'post-reveal'; await runWindow(S);
    S.R = resolvePlay(S);
    if (S.R.text) log(S.R.text, S.R.td ? 'big' : '');
    S.window = 'post-snap'; await runWindow(S);
    return applyResult(S);
  }
  function resolvePlay(S: Snap): SnapResult {
    const { id, type } = playInfo(S);
    let y = playInfo(S).yards;
    const read = S.read!, readType = read === 'run' ? 'run' : 'pass';
    let stuffed = readType === type;
    if (id === '116' && read === 'blitz') stuffed = false;
    if (id === '107') stuffed = read !== 'run';
    if (id === '163') stuffed = read === 'pass';
    if (S.zoneBlitz && read === 'blitz' && type === 'run') stuffed = true;
    if (S.goalLine && read === 'run' && type === 'pass') stuffed = true;
    const R: SnapResult = { stuffed: false, yards: 0 };
    if (id === '159') {
      if (S.prevent) { R.stuffed = true; R.text = 'Prevent defense knocks the Hail Mary down.'; return R; }
      if (read === 'run') { R.td = true; R.text = 'HAIL MARY... CAUGHT! Touchdown!'; return R; }
      R.stuffed = true; R.text = 'Hail Mary falls incomplete.'; return R;
    }
    if (id === '163' && S.prevent) { R.stuffed = true; R.text = 'Prevent defense sniffs out the Flea Flicker.'; return R; }
    if (stuffed && S.hotRoute && read === 'blitz' && type === 'pass') { stuffed = false; y = Math.max(playInfo(S).readYards, Math.floor(y / 2)); log('Hot Route beats the blitz.', 'off'); }
    R.stuffed = stuffed;
    // A correct read holds the play to its read yards (D20). On a 2-point try there's no room: a read stops it (D22).
    if (stuffed) { R.yards = S.isTry ? 0 : playInfo(S).readYards; return R; }
    if (type === 'run' && read === 'blitz' && !S.stunt) y += 3;
    if ((id === '106' || id === '116') && read === 'blitz') y += 6;
    if (S.playAction && type === 'pass' && read === 'run') y *= 2;
    if (S.bracket && type === 'pass') y = Math.floor(y / 2);
    if (S.spy && type === 'run') y = Math.floor(y / 2);
    if (S.prevent && type === 'pass') y = Math.min(12, y);
    R.yards = y;
    return R;
  }
  function discardPlay(S: Snap) { if (S.play && !S.play.base) toDiscard(S.offP, S.play); }
  function afterSnapDraws(S: Snap) { draw(S.offP, 'plays', 1); draw(S.defP, 'dspec', 1); }
  function applyResult(S: Snap): DriveEnd {
    const d = S.d, R = S.R!;
    discardPlay(S);
    if (S.isTry) {
      if (R.negated) return null;
      const good = !R.turnover && (R.td || R.yards >= d.ytg || R.autoFirst);
      return { type: good ? 'good' : 'nogood' };
    }
    // Every snap draws, including one that ends the drive (doc: offense draws after each play,
    // defense after each snap); otherwise a hand above its drive-start refill target loses a card.
    afterSnapDraws(S);
    if (R.negated) return null;
    if (R.defScore) { S.defP.score += R.defScore; return { type: 'turnover', text: 'Pick six.' }; }
    if (R.turnover) return { type: 'turnover', text: R.turnover === 'int' ? 'Interception.' : 'Fumble.' };
    if (R.td) return { type: 'td' };
    const gain = R.yards + (R.bonus || 0);
    d.ytg -= gain;
    d.dist -= gain;
    if (!R.text) log(R.stuffed ? (gain < 0 ? `Loss of ${-gain}.` : gain === 0 ? 'Read. No gain.' : `Read. Held to ${gain}.`) : gain < 0 ? `Loss of ${-gain}.` : `Gain of ${gain}.`);
    if (d.ytg <= 0) return { type: 'td' };
    if (d.ytg >= 100) return { type: 'safety' };
    // A correct read holds the level and banks +1 for the defense, even when the play gains (D21).
    if (R.levelReset) d.off.level = 1;
    else if (!R.stuffed || S.forceLevel) d.off.level = Math.min(5, d.off.level + 1);
    if (R.stuffed || gain < 0) d.def.bank = Math.min(5, d.def.bank + 1);
    d.off.bank = Math.min(6, d.off.bank + d.off.level);
    d.lastSuccess = !R.stuffed;
    d.prevRead = S.read;
    if (R.replayDown) return null;
    if (d.dist <= 0 || R.autoFirst) { d.down = 1; d.dist = Math.min(10, d.ytg); draw(S.offP, 'ospec', 1); log('First down.', 'off'); return null; }
    d.down++;
    if (d.down > 4) return { type: 'downs' };
    return null;
  }
  function finishAbort(S: Snap): DriveEnd {
    const d = S.d, a = S.abort!;
    d.off.bank += S.playCost;
    if (S.play && !S.play.base) toHand(S.offP, S.play);
    log(a.text, 'big');
    if (a.type === 'td' || a.type === 'good') return { type: a.type };
    d.off.level = Math.min(5, d.off.level + 1);
    d.off.bank = Math.min(6, d.off.bank + d.off.level);
    d.down = 1;
    d.dist = Math.min(10, d.ytg);
    afterSnapDraws(S);
    draw(S.offP, 'ospec', 1);
    return null;
  }
  async function attemptFG(S: Snap, boost: boolean): Promise<DriveEnd> {
    const d = S.d;
    S.isKick = true;
    S.kick = { dist: d.ytg + 17, boost, bigLeg: false, iced: false, blocked: false, six: false };
    if (boost) d.off.bank -= 2;
    log(`${S.offP.name} line${S.offP.human ? '' : 's'} up for a ${S.kick.dist}-yard field goal${boost ? ' (+10 range)' : ''}.`, 'off');
    S.window = 'pre-snap';
    await runWindow(S);
    const k = S.kick;
    const range = 40 + (k.iced ? 0 : (k.boost ? 10 : 0) + (k.bigLeg ? 10 : 0));
    await pause();
    if (k.blocked) {
      if (k.six) { S.defP.score += 7; log('KICK SIX! Blocked and returned for a touchdown.', 'def big'); return { type: 'turnover', text: '' }; }
      log('BLOCKED! No good.', 'def big');
      return { type: 'missed' };
    }
    if (k.iced) log('The kicker is iced: no range bonus.', 'def');
    if (k.dist <= range) { log(`The kick is GOOD from ${k.dist}.`, 'big'); return { type: 'fg' }; }
    log(`No good: ${k.dist} yards is beyond the ${range}-yard range.`, 'off');
    return { type: 'missed' };
  }

  /* ---------- drives, tries, game ---------- */
  const newDrive = (offIdx: number, ytg: number, label: string): Drive => ({
    offIdx, ytg, down: 1, dist: Math.min(10, ytg), off: { level: 1, bank: 1 }, def: { bank: 1 },
    sticky: false, hurry: false, prevRead: null, lastSuccess: false, filmStudy: false, label,
  });
  function dealDrive(offIdx: number) {
    drawUpTo(G.players[offIdx], 'plays', 5);
    drawUpTo(G.players[offIdx], 'ospec', 3);
    drawUpTo(G.players[1 - offIdx], 'dspec', 4);
  }
  async function playDrive(offIdx: number, ytg: number, label: string) {
    const offP = G.players[offIdx], defP = G.players[1 - offIdx];
    const d = newDrive(offIdx, ytg, label);
    G.d = d;
    dealDrive(offIdx);
    log(`${label}: ${offP.name} ball, ${spot(ytg)}`, 'head');
    let end: DriveEnd = null;
    for (let guard = 0; guard < 60 && !end; guard++) end = await playSnap(d);
    if (!end) end = { type: 'downs' };
    switch (end.type) {
      case 'td': offP.score += 6; log(`TOUCHDOWN, ${offP.name}!`, 'big'); await afterTouchdown(d); break;
      case 'fg': offP.score += 3; break;
      case 'turnover': if (end.text) log(end.text, 'def'); break;
      case 'downs': log('Turnover on downs.', 'def'); break;
      case 'safety': defP.score += 2; log('SAFETY! Two points for the defense.', 'def big'); break;
      default: break;
    }
    G.S = null;
    notify();
    await pause();
    await pause();
  }
  async function runTry(d: Drive) {
    d.ytg = 3; d.dist = 3; d.down = 1;
    for (let k = 0; k < 4; k++) {
      const r = await playSnap(d, true);
      if (r) return r.type === 'good';
    }
    return false;
  }
  async function afterTouchdown(d: Drive) {
    const offP = G.players[d.offIdx], defP = G.players[1 - d.offIdx];
    let goTwo: boolean;
    if (offP.human) {
      goTwo = await ask<boolean>({ kind: 'pat', title: 'Extra point', text: 'Kick is automatic for 1. A 2-point try is one snap from the 3.', cards: [], buttons: [{ label: 'Kick (1)', value: false, primary: true }, { label: 'Go for 2', value: true }] });
    } else {
      await pause();
      const diff = offP.score - defP.score;
      goTwo = diff === -2 || diff === -5 || diff === 1 || (offP.hand.ospec.some((i) => i.id === '169') && rnd() < 0.5);
    }
    if (!goTwo) { offP.score += 1; log('Extra point is good.'); return; }
    log(`${offP.name} go${offP.human ? '' : 'es'} for two.`, 'off');
    const ok = await runTry(d);
    if (ok) { offP.score += 2; log('Two-point try is GOOD.', 'big'); } else log('Two-point try fails.', 'def');
  }
  async function shootoutTry(offIdx: number) {
    const offP = G.players[offIdx];
    const d = newDrive(offIdx, 3, '2-point shootout');
    G.d = d;
    dealDrive(offIdx);
    log(`2-point shootout: ${offP.name}`, 'head');
    const ok = await runTry(d);
    if (ok) { offP.score += 2; log('GOOD.', 'big'); } else log('No good.', 'def');
    G.S = null;
    notify();
    await pause();
  }
  async function run() {
    const [a, b] = G.players;
    log(`Coin toss: ${G.players[G.first].name} receive${G.players[G.first].human ? '' : 's'} first.`, 'sys');
    for (let i = 0; i < 10; i++) {
      G.poss = i + 1;
      await playDrive((G.first + i) % 2, 75, `Possession ${i + 1} of 10`);
    }
    // College overtime: two rounds from the 25, then alternating 2-point tries.
    let round = 0;
    while (a.score === b.score && round < 8) {
      round++;
      G.ot = round;
      for (let k = 0; k < 2; k++) {
        const off = (G.first + k) % 2;
        if (round < 3) await playDrive(off, 25, `Overtime ${round}`);
        else await shootoutTry(off);
      }
    }
    G.over = true;
    G.d = null;
    G.S = null;
    const winner = a.score > b.score ? a : b;
    log(a.score === b.score ? 'Final: a tie.' : `Final: ${winner.name} win${winner.human ? '' : 's'}, ${Math.max(a.score, b.score)}-${Math.min(a.score, b.score)}.`, 'big');
  }

  return {
    state: G,
    get prompt() { return pending; },
    resolve(value: unknown) { pending?.resolve(value); },
    run,
    abandon() {
      abandoned = true;
      pending = null;
      // Settle an open prompt so run() rejects with Abandoned instead of hanging.
      rejectPending?.(new Abandoned());
      rejectPending = null;
    },
  };
}
