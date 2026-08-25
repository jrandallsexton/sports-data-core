// Stat-line building + sorting for the mobile roster builder.
//
// Mobile renders each athlete as a CARD with two aligned season lines
// ('26 over '25) instead of the web's mirrored table columns, but the
// underlying stat sets and the sort semantics mirror sd-ui's
// gridColumns.js / athleteSort.js — keep them in lockstep.

import type { PickemAthlete, SeasonBlock } from '@/src/services/api/playerPickemApi';

type StatPart = {
  key: string;
  label: string;
  value: (s: SeasonBlock | null, row: PickemAthlete) => number | null;
  fmt: (v: number) => string;
  // Whole-season formatter (e.g. "9/10" for FG) wins over fmt(value).
  fmtSeason?: (s: SeasonBlock | null) => string | null;
};

const int = (v: number) => Math.round(v).toLocaleString();
const oneDp = (v: number) => v.toFixed(1);

function stat(s: SeasonBlock | null, key: string): number | null {
  return s?.stats?.[key] ?? null;
}

const PER_GAME_KEY: Record<string, string> = {
  QB: 'passYdsPerGame',
  RB: 'rushYdsPerGame',
  WR: 'recYdsPerGame',
  TE: 'recYdsPerGame',
};

const TD_KEY: Record<string, string> = {
  QB: 'passTd',
  RB: 'rushTd',
  WR: 'recTd',
  TE: 'recTd',
};

export const STAT_PARTS: Record<string, StatPart[]> = {
  QB: [
    { key: 'cmpPct', label: 'CMP%', value: (s) => stat(s, 'cmpPct'), fmt: oneDp },
    { key: 'passYds', label: 'YDS', value: (s) => stat(s, 'passYds'), fmt: int },
    { key: 'passYdsPerGame', label: 'Y/G', value: (s) => stat(s, 'passYdsPerGame'), fmt: oneDp },
    { key: 'passTd', label: 'TD', value: (s) => stat(s, 'passTd'), fmt: int },
    { key: 'interceptions', label: 'INT', value: (s) => stat(s, 'interceptions'), fmt: int },
    { key: 'rushYds', label: 'RUSH', value: (s) => stat(s, 'rushYds'), fmt: int },
  ],
  RB: [
    { key: 'rushAtt', label: 'ATT', value: (s) => stat(s, 'rushAtt'), fmt: int },
    { key: 'rushYds', label: 'YDS', value: (s) => stat(s, 'rushYds'), fmt: int },
    { key: 'rushYdsPerGame', label: 'Y/G', value: (s) => stat(s, 'rushYdsPerGame'), fmt: oneDp },
    { key: 'rushTd', label: 'TD', value: (s) => stat(s, 'rushTd'), fmt: int },
    { key: 'receptions', label: 'REC', value: (s) => stat(s, 'receptions'), fmt: int },
  ],
  WR: [
    { key: 'receptions', label: 'REC', value: (s) => stat(s, 'receptions'), fmt: int },
    { key: 'recYds', label: 'YDS', value: (s) => stat(s, 'recYds'), fmt: int },
    { key: 'recYdsPerGame', label: 'Y/G', value: (s) => stat(s, 'recYdsPerGame'), fmt: oneDp },
    { key: 'recTd', label: 'TD', value: (s) => stat(s, 'recTd'), fmt: int },
  ],
  K: [
    {
      key: 'fg', label: 'FG', value: (s) => stat(s, 'fgMade'), fmt: int,
      fmtSeason: (s) => (s?.stats ? `${s.stats.fgMade}/${s.stats.fgAtt}` : null),
    },
    { key: 'fgPct', label: 'PCT', value: (s) => stat(s, 'fgPct'), fmt: oneDp },
    { key: 'fgLong', label: 'LNG', value: (s) => stat(s, 'fgLong'), fmt: int },
    {
      key: 'xp', label: 'XP', value: (s) => stat(s, 'xpMade'), fmt: int,
      fmtSeason: (s) => (s?.stats ? `${s.stats.xpMade}/${s.stats.xpAtt}` : null),
    },
  ],
  FLEX: [
    {
      key: 'flexYdsPerGame', label: 'Y/G',
      value: (s, row) => stat(s, PER_GAME_KEY[row.position]), fmt: oneDp,
    },
    {
      key: 'flexTd', label: 'TD',
      value: (s, row) => stat(s, TD_KEY[row.position]), fmt: int,
    },
  ],
};

STAT_PARTS.TE = STAT_PARTS.WR;

export function statPartsFor(slotId: string, positions: string[]): StatPart[] {
  return STAT_PARTS[slotId === 'FLEX' ? 'FLEX' : positions[0]] ?? [];
}

/** One season's compact card line: "68.6 CMP% · 1,247 YDS · 11 TD". */
export function seasonLine(
  parts: StatPart[],
  season: SeasonBlock | null,
  row: PickemAthlete
): string {
  return parts
    .map((p) => {
      if (p.fmtSeason) {
        const s = p.fmtSeason(season);
        return s == null ? `— ${p.label}` : `${s} ${p.label}`;
      }
      const v = p.value(season, row);
      return v == null ? `— ${p.label}` : `${p.fmt(v)} ${p.label}`;
    })
    .join(' · ');
}

/**
 * Case-insensitive substring match on last name, first name, or team
 * name. Empty/whitespace filter returns the SAME array so memo deps stay
 * cheap. Mirrors sd-ui's athleteSort.filterAthletes.
 */
export function filterAthletes(rows: PickemAthlete[], text: string): PickemAthlete[] {
  const needle = text.trim().toLowerCase();
  if (!needle) return rows;
  return rows.filter(
    (r) =>
      r.lastName.toLowerCase().includes(needle) ||
      r.firstName.toLowerCase().includes(needle) ||
      r.teamName.toLowerCase().includes(needle)
  );
}

/**
 * Case-insensitive substring match on the WEEK OPPONENT's name — the
 * matchup-hunting filter ("show me every RB playing UMass"). Bye-week
 * rows never match a non-empty filter. Mirrors sd-ui's filterByOpponent.
 */
export function filterByOpponent(rows: PickemAthlete[], text: string): PickemAthlete[] {
  const needle = text.trim().toLowerCase();
  if (!needle) return rows;
  return rows.filter((r) =>
    (r.opponentName ?? '').toLowerCase().includes(needle)
  );
}

export type SortDescriptor = { key: string; dir?: 'desc' | 'asc' };

export const NAME_SORT: SortDescriptor = { key: 'name' };

function byName(a: PickemAthlete, b: PickemAthlete) {
  return (
    a.lastName.localeCompare(b.lastName) ||
    a.firstName.localeCompare(b.firstName)
  );
}

/**
 * Sort semantics mirror sd-ui's athleteSort.js: order by current-season
 * values; when EVERY row's current value is null (week 1) fall back to
 * previous season automatically; nulls sink in both directions; ties
 * break by name. 'oppDef' sorts the row-level opponent number.
 */
export function sortAthletes(
  rows: PickemAthlete[],
  sort: SortDescriptor,
  parts: StatPart[]
): PickemAthlete[] {
  const next = [...rows];

  if (sort.key === 'name') {
    next.sort(byName);
    return next;
  }

  let basisOf: (row: PickemAthlete) => number | null;
  if (sort.key === 'oppDef') {
    basisOf = (row) => row.opponentDefPerGame;
  } else {
    const part = parts.find((p) => p.key === sort.key);
    if (!part) {
      next.sort(byName);
      return next;
    }
    const currentOf = (row: PickemAthlete) => part.value(row.currentSeason, row);
    const useFallback = next.every((row) => currentOf(row) == null);
    basisOf = useFallback
      ? (row) => part.value(row.previousSeason, row)
      : currentOf;
  }

  const dir = sort.dir === 'asc' ? 1 : -1;
  next.sort((a, b) => {
    const av = basisOf(a);
    const bv = basisOf(b);
    if (av == null && bv == null) return byName(a, b);
    if (av == null) return 1;
    if (bv == null) return -1;
    return av === bv ? byName(a, b) : (av - bv) * dir;
  });
  return next;
}
