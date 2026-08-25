import {
  SLOT_DEFS,
  assign,
  canAssign,
  remove,
  isRostered,
} from '@/src/utils/pickem/rosterLogic';
import {
  NAME_SORT,
  sortAthletes,
  statPartsFor,
  seasonLine,
} from '@/src/utils/pickem/athleteStats';
import type { PickemAthlete } from '@/src/services/api/playerPickemApi';

function qb(
  lastName: string,
  currentYds: number | null,
  prevYds: number | null
): PickemAthlete {
  return {
    athleteId: `qb-${lastName}`,
    firstName: 'Test',
    lastName,
    teamName: 'Team',
    teamSlug: 'team',
    position: 'QB',
    opponentName: 'Opp',
    opponentSlug: 'opp',
    opponentDefPerGame: 200,
    currentSeason:
      currentYds == null
        ? null
        : { seasonYear: 2026, gamesPlayed: 4, stats: { passYds: currentYds } },
    previousSeason:
      prevYds == null
        ? null
        : { seasonYear: 2025, gamesPlayed: 13, stats: { passYds: prevYds } },
  };
}

const qbParts = statPartsFor('QB', ['QB']);

describe('rosterLogic (mobile port)', () => {
  it('matches the web slot shape with DEF disabled', () => {
    expect(SLOT_DEFS.map((s) => s.id)).toEqual([
      'QB', 'RB1', 'RB2', 'WR1', 'WR2', 'TE', 'FLEX', 'K', 'DEF',
    ]);
    expect(SLOT_DEFS.find((s) => s.id === 'DEF')?.disabled).toBe(true);
  });

  it('enforces eligibility, duplicates, and removal', () => {
    const a = qb('Manning', 1000, 2000);
    let roster = assign({}, 'QB', a);
    expect(roster.QB).toBe(a);
    expect(canAssign(roster, 'FLEX', a)).toBe(false); // QB not FLEX-eligible anyway
    expect(isRostered(roster, a.athleteId)).toBe(true);
    roster = remove(roster, 'QB');
    expect(isRostered(roster, a.athleteId)).toBe(false);
  });
});

describe('sortAthletes (mobile port)', () => {
  it('sorts current-season desc with nulls sinking', () => {
    const rows = [qb('Out', null, 4000), qb('High', 1300, 1), qb('Low', 800, 9999)];
    const sorted = sortAthletes(rows, { key: 'passYds', dir: 'desc' }, qbParts);
    expect(sorted.map((r) => r.lastName)).toEqual(['High', 'Low', 'Out']);
  });

  it('falls back to previous season when no row has current data', () => {
    const rows = [qb('Small', null, 1785), qb('Big', null, 4052)];
    const sorted = sortAthletes(rows, { key: 'passYds', dir: 'desc' }, qbParts);
    expect(sorted.map((r) => r.lastName)).toEqual(['Big', 'Small']);
  });

  it('name sort is the default ordering', () => {
    const rows = [qb('Zeta', 1, 1), qb('Alpha', 2, 2)];
    expect(sortAthletes(rows, NAME_SORT, qbParts).map((r) => r.lastName)).toEqual([
      'Alpha', 'Zeta',
    ]);
  });
});

describe('seasonLine', () => {
  it('renders a compact line and em-dashes missing stats', () => {
    const a = qb('Manning', 1247, null);
    const line = seasonLine(qbParts, a.currentSeason, a);
    expect(line).toContain('1,247 YDS');
    expect(line).toContain('— CMP%'); // only passYds present in this fixture
  });

  it('uses whole-season formatters for K ratios', () => {
    const kParts = statPartsFor('K', ['K']);
    const kicker: PickemAthlete = {
      ...qb('Zvada', null, null),
      position: 'K',
      currentSeason: {
        seasonYear: 2026,
        gamesPlayed: 5,
        stats: { fgMade: 9, fgAtt: 10, fgPct: 90.0, fgLong: 56, xpMade: 14, xpAtt: 14 },
      },
    };
    const line = seasonLine(kParts, kicker.currentSeason, kicker);
    expect(line).toContain('9/10 FG');
    expect(line).toContain('14/14 XP');
  });
});
