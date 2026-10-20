import type { CardCategory } from '@/src/utils/cardgame/types';

// The card game keeps its own paper-and-ink look in both app themes: it's a
// card table, and the card faces are designed as printed cards. Values come
// from the prototype's tokens (docs/features/card-game/play.html), converted
// from oklch.
export const CG = {
  table: '#e9e2d2',
  paper: '#f6efdd',
  ink: '#17140f',
  inkSoft: '#5a5246',
  chalk: '#fbf8f0',
  gold: '#f0bb3b',
  turf: '#36884d',
  turfDark: '#195c2e',
  warn: '#de3b3d',
  defChip: '#ff8e86',
  select: '#2a6fdb',
  ball: '#7a3f1d',
  chalkFaint: 'rgba(251, 248, 240, 0.45)',
} as const;

export const CATEGORY_COLOR: Record<CardCategory, string> = {
  play: '#4b99d7',
  modifier: '#47c86e',
  'power-play': '#f4514f',
  counter: '#a07cdb',
  read: '#f4bf3f',
  back: '#17140f',
};

/** Loaded by the card game screen only (app/card-game.tsx), not at app start. */
export const FONT_DISPLAY = 'AlfaSlabOne';
/** Already loaded app-wide in app/_layout.tsx. */
export const FONT_MONO = 'SpaceMono';
