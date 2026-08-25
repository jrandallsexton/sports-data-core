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
 * Without this, RN's Text wraps wherever width runs out, producing
 * orphans like "Chicago Sports\nNetwork" on phone-width cards. A single
 * name wider than the container still hard-breaks (RN's default), which
 * is the right fallback for a pathological input.
 */
export function formatBroadcasts(broadcasts: string): string {
  return broadcasts
    .split('|')
    .map((name) => name.trim().replace(/ /g, NBSP))
    .filter(Boolean)
    .join(' | ');
}
