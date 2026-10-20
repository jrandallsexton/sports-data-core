import { fitHand, GRID_GAP, HAND_PAD_X } from '@/src/components/features/cardgame/handLayout';

// Offense on a typical phone: 5 plays + 2 base, 3 specials.
const offense = { sections: [7, 3] };

describe('fitHand', () => {
  it('never needs a sideways scroll', () => {
    for (const width of [320, 360, 390, 430]) {
      for (const height of [400, 560, 700]) {
        const l = fitHand({ width, height, ...offense });
        expect(l.cols * l.cardW + (l.cols - 1) * GRID_GAP[l.cols]).toBeLessThanOrEqual(width - HAND_PAD_X * 2);
      }
    }
  });

  it('keeps a forced column count and only adapts the height', () => {
    const l = fitHand({ width: 390, height: 400, ...offense, cols: 4 });
    expect(l.cols).toBe(4);
    expect(l.fits).toBe(false);
  });

  it('uses the biggest cards that fit', () => {
    const tall = fitHand({ width: 390, height: 2000, ...offense });
    expect(tall.cols).toBe(3);
    const phone = fitHand({ width: 390, height: 620, ...offense });
    expect(phone.cols).toBe(4);
    expect(phone.fits).toBe(true);
  });

  it('gives spare height to the cards, up to 5:9', () => {
    const l = fitHand({ width: 390, height: 680, ...offense });
    expect(l.cardH).toBeGreaterThan(Math.round(l.cardW * 1.4));
    expect(l.cardH).toBeLessThanOrEqual(Math.round(l.cardW * 1.8));
  });

  it('falls back to 5 across and scrolls when nothing fits', () => {
    const l = fitHand({ width: 390, height: 200, ...offense });
    expect(l.cols).toBe(5);
    expect(l.fits).toBe(false);
    expect(l.cardH).toBe(Math.round(l.cardW * 1.4));
  });
});
