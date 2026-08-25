import { describe, it, expect } from 'vitest';
import {
  NAME_SORT,
  sortAthletes,
  filterAthletes,
  filterByOpponent,
} from './athleteSort';

describe('filterByOpponent', () => {
  const rows = [
    { lastName: 'Love', opponentName: 'UMass Minutemen' },
    { lastName: 'Singleton', opponentName: 'UCLA Bruins' },
    { lastName: 'OnBye', opponentName: null },
  ];

  it('matches the week opponent case-insensitively', () => {
    const hits = filterByOpponent(rows, 'umass');
    expect(hits).toHaveLength(1);
    expect(hits[0].lastName).toBe('Love');
  });

  it('bye rows never match a non-empty filter', () => {
    expect(filterByOpponent(rows, 'u')).toHaveLength(2);
  });

  it('returns the SAME array for an empty filter', () => {
    expect(filterByOpponent(rows, '')).toBe(rows);
  });
});

describe('filterAthletes', () => {
  const rows = [
    { firstName: 'Arch', lastName: 'Manning', teamName: 'Texas Longhorns' },
    { firstName: 'Garrett', lastName: 'Nussmeier', teamName: 'LSU Tigers' },
    { firstName: 'Cade', lastName: 'Klubnik', teamName: 'Clemson Tigers' },
  ];

  it('matches last name, first name, and team name case-insensitively', () => {
    expect(filterAthletes(rows, 'manning')).toHaveLength(1);
    expect(filterAthletes(rows, 'ARCH')).toHaveLength(1);
    expect(filterAthletes(rows, 'tigers')).toHaveLength(2);
  });

  it('returns the SAME array for an empty or whitespace filter', () => {
    expect(filterAthletes(rows, '')).toBe(rows);
    expect(filterAthletes(rows, '   ')).toBe(rows);
  });

  it('returns empty for no match', () => {
    expect(filterAthletes(rows, 'zzz')).toHaveLength(0);
  });
});

// getValue mirrors the grid's column accessors: read one stat off a
// season block.
const passYds = (row, season) => season?.stats?.passYds ?? null;

function qbRow(lastName, currentYds, prevYds) {
  return {
    firstName: 'Test',
    lastName,
    currentSeason:
      currentYds == null ? null : { stats: { passYds: currentYds } },
    previousSeason: prevYds == null ? null : { stats: { passYds: prevYds } },
  };
}

describe('sortAthletes', () => {
  it('name sort orders by last name then first name', () => {
    const rows = [
      { firstName: 'B', lastName: 'Zeta' },
      { firstName: 'A', lastName: 'Alpha' },
      { firstName: 'A', lastName: 'Zeta' },
    ];
    const sorted = sortAthletes(rows, NAME_SORT);
    expect(sorted.map((r) => `${r.lastName}-${r.firstName}`)).toEqual([
      'Alpha-A',
      'Zeta-A',
      'Zeta-B',
    ]);
  });

  it('stat desc orders by CURRENT-season values when any exist', () => {
    const rows = [
      qbRow('Low', 800, 4000),
      qbRow('High', 1300, 1000),
      qbRow('Mid', 1100, 2000),
    ];
    const sorted = sortAthletes(rows, { key: 'passYds', dir: 'desc' }, passYds);
    expect(sorted.map((r) => r.lastName)).toEqual(['High', 'Mid', 'Low']);
  });

  it('sinks null-current rows to the bottom in both directions', () => {
    const rows = [qbRow('Out', null, 4000), qbRow('Playing', 900, 500)];
    expect(
      sortAthletes(rows, { key: 'passYds', dir: 'desc' }, passYds)[1].lastName
    ).toBe('Out');
    expect(
      sortAthletes(rows, { key: 'passYds', dir: 'asc' }, passYds)[1].lastName
    ).toBe('Out');
  });

  it('falls back to PREVIOUS season when no row has current data (week 1)', () => {
    const rows = [
      qbRow('Small', null, 1785),
      qbRow('Big', null, 4052),
      qbRow('Mid', null, 2885),
    ];
    const sorted = sortAthletes(rows, { key: 'passYds', dir: 'desc' }, passYds);
    expect(sorted.map((r) => r.lastName)).toEqual(['Big', 'Mid', 'Small']);
  });

  it('does NOT fall back when even one row has current data', () => {
    // Mixed slate: one player has kicked off. Prior-year monsters must not
    // outrank live production.
    const rows = [qbRow('Vet', null, 4052), qbRow('Live', 320, 900)];
    const sorted = sortAthletes(rows, { key: 'passYds', dir: 'desc' }, passYds);
    expect(sorted.map((r) => r.lastName)).toEqual(['Live', 'Vet']);
  });

  it('ties break by name and the input array is not mutated', () => {
    const rows = [qbRow('Zeta', 1000, 1), qbRow('Alpha', 1000, 2)];
    const sorted = sortAthletes(rows, { key: 'passYds', dir: 'desc' }, passYds);
    expect(sorted.map((r) => r.lastName)).toEqual(['Alpha', 'Zeta']);
    expect(rows.map((r) => r.lastName)).toEqual(['Zeta', 'Alpha']);
  });

  it('falls back to name sort when no getValue is supplied', () => {
    const rows = [qbRow('Zeta', 1, 1), qbRow('Alpha', 2, 2)];
    const sorted = sortAthletes(rows, { key: 'passYds', dir: 'desc' });
    expect(sorted.map((r) => r.lastName)).toEqual(['Alpha', 'Zeta']);
  });
});
