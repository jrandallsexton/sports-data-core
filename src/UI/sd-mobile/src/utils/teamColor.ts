/**
 * Team colors for the comparison surfaces. Mirrors sd-ui TeamComparison.jsx:
 * the API ships Franchise.ColorCodeHex with or without the leading "#", and a
 * favored value is painted ON the team color, so the text must flip to
 * whichever of light/dark reads against it.
 */

/** "#rrggbb" for a 3- or 6-digit hex with or without "#"; null otherwise. */
export function normalizeTeamColor(raw: string | null | undefined): string | null {
  if (!raw) return null;
  const hex = raw.trim().replace(/^#/, '');
  if (/^[0-9a-fA-F]{6}$/.test(hex)) return `#${hex.toLowerCase()}`;
  if (/^[0-9a-fA-F]{3}$/.test(hex)) {
    return `#${hex.split('').map((c) => c + c).join('').toLowerCase()}`;
  }
  return null;
}

/** Light or dark text for a "#rrggbb" background (relative-luminance cut at 128, as on web). */
export function contrastTextOn(hexBackground: string): string {
  const hex = hexBackground.replace(/^#/, '');
  const r = parseInt(hex.substring(0, 2), 16);
  const g = parseInt(hex.substring(2, 4), 16);
  const b = parseInt(hex.substring(4, 6), 16);
  const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
  return luminance < 128 ? '#ffffff' : '#23272f';
}

/**
 * Both teams' colors for a comparison surface. A missing or non-hex color
 * falls back to primaryFallback; when the two sides resolve to the SAME
 * color (both missing, or two teams that share one), the home side takes
 * the neutral instead - otherwise a split bar paints one unbroken color and
 * silently reads as "one side owns everything".
 */
export function resolveTeamColors(
  awayRaw: string | null | undefined,
  homeRaw: string | null | undefined,
  primaryFallback: string,
  neutralFallback: string,
): { away: string; home: string } {
  // Everything is compared in normalized form: a raw fallback like "#1B3A6B"
  // must collide with a team color of "1b3a6b", not slip past as a different
  // string that paints the same pixel.
  const primary = normalizeTeamColor(primaryFallback) ?? LAST_RESORT_PRIMARY;
  const neutral = normalizeTeamColor(neutralFallback) ?? LAST_RESORT_NEUTRAL;
  const away = normalizeTeamColor(awayRaw) ?? primary;
  const homeOwn = normalizeTeamColor(homeRaw) ?? primary;
  // First candidate that differs from away wins; the neutral itself can
  // collide (a gray team on a gray theme), so keep going down the list.
  const home =
    [homeOwn, neutral, primary, LAST_RESORT_NEUTRAL, LAST_RESORT_PRIMARY].find((c) => c !== away) ?? homeOwn;
  return { away, home };
}

const LAST_RESORT_PRIMARY = '#1b3a6b';
const LAST_RESORT_NEUTRAL = '#9ca3af';
