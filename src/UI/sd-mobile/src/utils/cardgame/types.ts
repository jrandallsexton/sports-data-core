// Football card game: data and state shapes. The rules live in
// docs/features/card-game/football-card-game.md; cards.json there is the
// source of truth and is copied next to this file.

export type Side = 'off' | 'def';
export type ReadKind = 'run' | 'pass' | 'blitz';
export type PlayType = 'run' | 'pass';
export type HandKey = 'plays' | 'ospec' | 'dspec';
export type CardWindow = 'commit' | 'drive' | 'pre-snap' | 'post-reveal' | 'post-snap';
export type CardCategory = 'play' | 'modifier' | 'power-play' | 'counter' | 'read' | 'back';

export type CardCost = { kind: 'fixed'; value: number } | { kind: 'all'; min: number };

export interface CardDef {
  id: string;
  slug: string;
  name: string;
  side: 'offense' | 'defense' | 'either';
  category: CardCategory;
  cost: CardCost | null;
  window: CardWindow | null;
  play: { type: PlayType; yards: number | null; readYards?: number } | null;
  rulesText: string;
  reminderText: string;
  flavorText: string;
  counters: string[];
  copies: Record<string, number>;
}

/** One physical card. Base plays and reads are shared, reusable instances. */
export interface CardInst {
  uid: string;
  id: string;
  /** The hand this card was drawn into. */
  k?: HandKey;
  base?: boolean;
  read?: ReadKind;
  isSpecialPlay?: boolean;
}

export interface Pile {
  draw: CardInst[];
  discard: CardInst[];
}

export interface Player {
  name: string;
  human: boolean;
  score: number;
  decks: Record<HandKey, Pile>;
  hand: Record<HandKey, CardInst[]>;
}

/** One possession (or a 2-point shootout try). ytg = yards to the goal line. */
export interface Drive {
  offIdx: number;
  ytg: number;
  down: number;
  dist: number;
  off: { level: number; bank: number };
  def: { bank: number };
  sticky: boolean;
  hurry: boolean;
  prevRead: ReadKind | null;
  lastSuccess: boolean;
  filmStudy: boolean;
  label: string;
}

/** The snap's outcome, built at resolve and edited by post-snap cards. */
export interface SnapResult {
  stuffed: boolean;
  yards: number;
  text?: string;
  td?: boolean;
  sacked?: boolean;
  levelReset?: boolean;
  turnover?: 'fumble' | 'int' | null;
  hijacked?: boolean;
  defScore?: number;
  bonus?: number;
  autoFirst?: boolean;
  negated?: boolean;
  replayDown?: boolean;
}

export interface Kick {
  dist: number;
  boost: boolean;
  bigLeg: boolean;
  iced: boolean;
  blocked: boolean;
  six: boolean;
}

/** One snap in progress. Flags are set by the cards played during it. */
export interface Snap {
  d: Drive;
  offP: Player;
  defP: Player;
  isTry: boolean;
  read: ReadKind | null;
  play: CardInst | null;
  playCost: number;
  R: SnapResult | null;
  window: CardWindow | null;
  revealed?: boolean;
  readShown?: boolean;
  gotchya?: boolean;
  abort?: { type: 'td' | 'good' | 'firstdown'; text: string };
  isKick?: boolean;
  kick?: Kick;
  playAction?: boolean;
  hotRoute?: boolean;
  forceLevel?: boolean;
  stunt?: boolean;
  bracket?: boolean;
  goalLine?: boolean;
  zoneBlitz?: boolean;
  spy?: boolean;
  prevent?: boolean;
}

export type LogClass = '' | 'off' | 'def' | 'sys' | 'big' | 'head' | 'def big';

export interface LogEntry {
  text: string;
  cls: LogClass;
  seq: number;
}

export interface GameState {
  players: [Player, Player];
  log: LogEntry[];
  seq: number;
  /** First log seq of the current snap, and of the one before it (the feed falls back to it). */
  playAt: number;
  prevPlayAt: number;
  first: number;
  poss: number;
  ot: number;
  over: boolean;
  d: Drive | null;
  S: Snap | null;
}

export type CommitChoice =
  | { kind: 'play'; inst: CardInst }
  | { kind: 'kick'; boost: boolean };

export interface PromptButton {
  label: string;
  value: unknown;
  primary?: boolean;
}

/** A decision the human has to make. Resolve with a card from `cards` or a button's value. */
export interface Prompt {
  kind: 'read' | 'commit' | 'window' | 'response' | 'swap' | 'pat';
  title: string;
  text?: string;
  cards: CardInst[];
  buttons: PromptButton[];
}
