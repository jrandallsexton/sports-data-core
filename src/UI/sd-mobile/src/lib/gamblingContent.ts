import type { PickType, UserOptions } from '@/src/types/models';

// Single choke point for gambling-content visibility (spreads, totals, odds).
// EVERY surface must route through this predicate rather than checking the
// raw user option — that keeps a future policy layer (e.g. an under-13 mode
// that force-hides regardless of preference) a one-function change.
// Mirrors sd-ui's utils/gamblingContent.js. See docs/features/user-options.md.

export function shouldShowGambling(
  pickType: PickType | null | undefined,
  options: UserOptions | null | undefined,
): boolean {
  // ATS / O-U leagues: the lines ARE the game — always show.
  if (pickType === 'AgainstTheSpread' || pickType === 'OverUnder') {
    return true;
  }
  // Straight-Up (or unknown) context: only when the user opted in. Missing
  // options (loading / fetch failure) mean the safe default: hidden.
  return options?.showGamblingContent === true;
}
