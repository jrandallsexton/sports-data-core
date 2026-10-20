// Card sizing for the game screen. The rules (owner calls, 2026-10-20):
// every card in the active hands is visible at once -- no sideways scroll --
// at the biggest size that fits (3 across, else 4, else 5), the read and play
// cards in the snap row match the hand's cards, and leftover height goes to
// all of them (up to 5:9) rather than sitting empty.

export const HAND_PAD_X = 10;
export const HAND_PAD_TOP = 8;
export const HAND_PAD_BOTTOM = 10;
export const SECTION_GAP = 10;
export const SECTION_HEADER = 17;
export const EMPTY_ROW = 20;
export const SNAP_CHROME = 8 + 8 + 2 + 14; // padding, bottom border, slot label
export const GRID_GAP: Record<number, number> = { 3: 10, 4: 8, 5: 6 };
const RATIO = 7 / 5;
const MAX_RATIO = 9 / 5;

export interface HandLayoutInput {
  /** Width of the screen area. */
  width: number;
  /** Height shared by the snap row and the hand (the prompt bar already subtracted). */
  height: number;
  /** Card count per active section. */
  sections: number[];
  /** Keep this column count (only the card height adapts). */
  cols?: number;
}

export interface HandLayout {
  cols: number;
  gap: number;
  /** Width of every card, hand and snap row. */
  cardW: number;
  /** Card height, hand and snap row (stretched into spare space). */
  cardH: number;
  /** False when even 5 across overflows; the hand then scrolls. */
  fits: boolean;
}

const sectionRows = (count: number, cols: number) => Math.ceil(count / cols);

function handHeight(sections: number[], cols: number, cardH: number, gap: number) {
  let h = HAND_PAD_TOP + HAND_PAD_BOTTOM;
  sections.forEach((count, i) => {
    const rows = sectionRows(count, cols);
    h += SECTION_HEADER + (rows ? rows * cardH + (rows - 1) * gap : EMPTY_ROW);
    if (i > 0) h += SECTION_GAP;
  });
  return h;
}

export function fitHand({ width, height, sections, cols: forced }: HandLayoutInput): HandLayout {
  let pick: HandLayout | null = null;
  for (const cols of forced ? [forced] : [3, 4, 5]) {
    const gap = GRID_GAP[cols];
    const cardW = Math.floor((width - HAND_PAD_X * 2 - gap * (cols - 1)) / cols);
    const snapH = Math.round(cardW * RATIO);
    const used = SNAP_CHROME + snapH + handHeight(sections, cols, snapH, gap);
    pick = { cols, gap, cardW, cardH: snapH, fits: used <= height };
    if (pick.fits) {
      // The snap row's cards count as one more row.
      const rows = 1 + sections.reduce((n, c) => n + sectionRows(c, cols), 0);
      pick.cardH = Math.min(snapH + Math.floor((height - used) / rows), Math.round(cardW * MAX_RATIO));
      break;
    }
  }
  return pick!;
}
