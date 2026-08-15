import {
  formatToUserTime,
  getDefaultGameWeekday,
} from '../../src/utils/timeUtils';

// 2026-08-15 is a Saturday; 2026-08-16 a Sunday; 2026-08-13 a Thursday.
// 17:00Z renders 1:00 PM in the default Eastern zone (EDT) — safely away
// from the midnight-means-TBD sentinel. Mirrors the web vitest suite.
const SAT = '2026-08-15T17:00:00Z';
const SUN = '2026-08-16T17:00:00Z';
const THU = '2026-08-13T17:00:00Z';

describe('getDefaultGameWeekday', () => {
  it('maps NCAAFB to Saturday and NFL to Sunday, from enum names or slugs', () => {
    expect(getDefaultGameWeekday('FootballNcaa')).toBe(6);
    expect(getDefaultGameWeekday('football-ncaa')).toBe(6);
    expect(getDefaultGameWeekday('FootballNfl')).toBe(7);
    expect(getDefaultGameWeekday('football-nfl')).toBe(7);
  });

  it('is null (always show the day) for sports without a dominant day', () => {
    expect(getDefaultGameWeekday('BaseballMlb')).toBeNull();
    expect(getDefaultGameWeekday(undefined)).toBeNull();
    expect(getDefaultGameWeekday(null)).toBeNull();
  });
});

describe('formatToUserTime day parenthetical', () => {
  it('suppresses the day only on the sport default day', () => {
    // NCAAFB (Sat default): Saturday bare, Thursday annotated
    expect(formatToUserTime(SAT, undefined, 6)).toBe('Aug 15 @ 1:00 PM');
    expect(formatToUserTime(THU, undefined, 6)).toBe('Aug 13 (Thu) @ 1:00 PM');

    // NFL (Sun default): Sunday bare, SATURDAY ANNOTATED — the preseason
    // regression this exists for
    expect(formatToUserTime(SUN, undefined, 7)).toBe('Aug 16 @ 1:00 PM');
    expect(formatToUserTime(SAT, undefined, 7)).toBe('Aug 15 (Sat) @ 1:00 PM');
  });

  it('always shows the day when the default is null', () => {
    expect(formatToUserTime(SAT, undefined, null)).toBe('Aug 15 (Sat) @ 1:00 PM');
  });

  it('keeps the legacy Saturday default when the parameter is omitted', () => {
    expect(formatToUserTime(SAT)).toBe('Aug 15 @ 1:00 PM');
    expect(formatToUserTime(THU)).toBe('Aug 13 (Thu) @ 1:00 PM');
  });
});
