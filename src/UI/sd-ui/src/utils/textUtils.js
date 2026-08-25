/**
 * Text-shaping helpers shared by card surfaces.
 */

// U+00A0 non-breaking space, constructed so no invisible literal hides in
// source for a formatter or copy-paste to silently normalize away.
const NBSP = String.fromCharCode(0x00a0);

/**
 * Reshape a pre-joined broadcast list ("Rangers Sports Network | MLB.TV |
 * Chicago Sports Network") so line wrapping can only happen at the
 * separators, never inside a network name. Each name's internal spaces
 * become non-breaking spaces; the " | " separators keep ordinary spaces
 * and remain the only legal break points.
 *
 * Mirrors sd-mobile's src/utils/textUtils.ts — on phone-width cards the
 * raw string wraps mid-name ("Chicago Sports" / "Network").
 */
export function formatBroadcasts(broadcasts) {
  return broadcasts
    .split('|')
    .map((name) => name.trim().replace(/ /g, NBSP))
    .filter(Boolean)
    .join(' | ');
}
