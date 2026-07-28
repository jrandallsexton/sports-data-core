// Single choke point for gambling-content visibility (spreads, totals, odds).
// EVERY surface must route through this predicate rather than checking the
// raw user option — that keeps a future policy layer (e.g. an under-13 mode
// that force-hides regardless of preference) a one-function change.
// See docs/features/user-options.md.

/**
 * @param {string|null|undefined} pickType League pick type wire value
 *   ("StraightUp" | "AgainstTheSpread" | "OverUnder" | unknown).
 * @param {{showGamblingContent?: boolean}|null|undefined} userOptions
 *   Typed options from UserContext; null while loading / on failure.
 * @returns {boolean} true when gambling content should render.
 */
export function shouldShowGambling(pickType, userOptions) {
  // ATS / O-U leagues: the lines ARE the game — always show.
  if (pickType === "AgainstTheSpread" || pickType === "OverUnder") {
    return true;
  }
  // Straight-Up (or unknown) context: only when the user opted in.
  // null options (loading / fetch failure) mean the safe default: hidden.
  return userOptions?.showGamblingContent === true;
}
