// Display helpers for the simulated 1-unit bet net (UserPicksResultDto.betPoints).
// Numbers only, no currency sign, by design.

/**
 * @param {number} value Week net, e.g. 2.2622 or -4.2143.
 * @returns {string} Signed, two places: "+2.26", "-4.21", "0.00".
 */
export function formatBetPoints(value) {
  const rounded = Math.round(value * 100) / 100;
  // Rounding can land on -0 (e.g. -0.004); show it as an even "0.00".
  if (rounded === 0) return "0.00";
  return `${rounded > 0 ? "+" : ""}${rounded.toFixed(2)}`;
}

/**
 * @param {number} value Week net.
 * @returns {"positive"|"negative"|"even"} Tone class, judged on the displayed value.
 */
export function betPointsTone(value) {
  const rounded = Math.round(value * 100) / 100;
  if (rounded > 0) return "positive";
  if (rounded < 0) return "negative";
  return "even";
}
