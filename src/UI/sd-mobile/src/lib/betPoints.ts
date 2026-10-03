// Display helpers for the simulated 1-unit bet net (UserPicksResult.betPoints).
// Numbers only, no currency sign, by design. Mirrors sd-ui's utils/betPoints.js.

export type BetPointsTone = 'positive' | 'negative' | 'even';

/** Signed, two places: "+2.26", "-4.21", "0.00". */
export function formatBetPoints(value: number): string {
  const rounded = Math.round(value * 100) / 100;
  // Rounding can land on -0 (e.g. -0.004); show it as an even "0.00".
  if (rounded === 0) return '0.00';
  return `${rounded > 0 ? '+' : ''}${rounded.toFixed(2)}`;
}

/** Tone judged on the displayed value, so "0.00" is never colored. */
export function betPointsTone(value: number): BetPointsTone {
  const rounded = Math.round(value * 100) / 100;
  if (rounded > 0) return 'positive';
  if (rounded < 0) return 'negative';
  return 'even';
}
