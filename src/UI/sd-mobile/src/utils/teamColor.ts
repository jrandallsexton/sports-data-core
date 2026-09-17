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
  // First candidate the eye can tell apart from away wins - not merely a
  // different string, and not merely a different shade. The neutral itself
  // can collide (a gray team on a gray theme), so keep going down the list.
  const home =
    [homeOwn, neutral, primary, LAST_RESORT_NEUTRAL, LAST_RESORT_PRIMARY]
      .find((c) => colorsDistinguishable(c, away)) ?? homeOwn;
  return { away, home };
}

/**
 * Can two "#rrggbb" team colors share a 5px bar and still be told apart?
 *
 * Team colors are distinguished by hue family, not by shade: USC (#9e2237)
 * and Rutgers (#d21034) are 114 apart in weighted RGB and still just "two
 * reds"; Georgia/Arkansas, crimson/dark red, Tennessee/Clemson orange are
 * the same story. So: two saturated colors are distinguishable when their
 * hues sit in different families (>= 24 degrees apart) or one is much
 * lighter than the other (navy vs powder blue). An achromatic color
 * (black, white, gray) is distinguishable from any real color, and from
 * another achromatic only by a lightness gap.
 *
 * About a fifth of 2026 NCAA matchups pair same-family colors (a lot of
 * red and navy in this sport); those render the home side in the neutral.
 * The durable fix is Franchise.ColorCodeAltHex, which ESPN ships and the
 * processor does not yet store.
 */
export function colorsDistinguishable(a: string, b: string): boolean {
  const [ha, sa, la] = hsl(a);
  const [hb, sb, lb] = hsl(b);
  const aAch = sa < ACHROMATIC_SATURATION || la < 0.12 || la > 0.9;
  const bAch = sb < ACHROMATIC_SATURATION || lb < 0.12 || lb > 0.9;
  if (aAch && bAch) return Math.abs(la - lb) >= LIGHTNESS_GAP;
  if (aAch !== bAch) return true;
  if (hueDistance(ha, hb) >= HUE_FAMILY_DEGREES) return true;
  return Math.abs(la - lb) >= LIGHTNESS_GAP;
}

/** Weighted-RGB ("redmean") distance, 0 (same) to ~765. Kept for callers that want a magnitude. */
export function colorDistance(a: string, b: string): number {
  const [r1, g1, b1] = channels(a);
  const [r2, g2, b2] = channels(b);
  const rMean = (r1 + r2) / 2;
  const dr = r1 - r2;
  const dg = g1 - g2;
  const db = b1 - b2;
  return Math.sqrt((2 + rMean / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rMean) / 256) * db * db);
}

function channels(hex: string): [number, number, number] {
  const h = hex.replace(/^#/, '');
  return [parseInt(h.substring(0, 2), 16), parseInt(h.substring(2, 4), 16), parseInt(h.substring(4, 6), 16)];
}

/** [hue 0-360, saturation 0-1, lightness 0-1] */
function hsl(hex: string): [number, number, number] {
  const [r, g, b] = channels(hex).map((c) => c / 255);
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const l = (max + min) / 2;
  const d = max - min;
  if (d === 0) return [0, 0, l];
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
  let h: number;
  if (max === r) h = ((g - b) / d + (g < b ? 6 : 0)) * 60;
  else if (max === g) h = ((b - r) / d + 2) * 60;
  else h = ((r - g) / d + 4) * 60;
  return [h, s, l];
}

function hueDistance(a: number, b: number): number {
  const d = Math.abs(a - b) % 360;
  return Math.min(d, 360 - d);
}

const HUE_FAMILY_DEGREES = 24;
const LIGHTNESS_GAP = 0.3;
const ACHROMATIC_SATURATION = 0.18;

const LAST_RESORT_PRIMARY = '#1b3a6b';
const LAST_RESORT_NEUTRAL = '#9ca3af';
