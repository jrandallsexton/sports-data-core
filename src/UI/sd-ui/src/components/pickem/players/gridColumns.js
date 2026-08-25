// Per-position stat column definitions for the roster-builder grid.
//
// Each column reads a season block ({ seasonYear, gamesPlayed, stats } or
// null) and formats one cell. The SAME columns render the current-season
// primary row and the previous-season sub-row, which is the point: prior
// season sits directly under current season for vertical comparison.
//
// FLEX merges RB/WR/TE, whose stat keys differ — its columns resolve the
// position-appropriate value per row (Y/G means rush for an RB, receiving
// for a WR/TE).

const int = (v) => (v == null ? null : Math.round(v).toLocaleString());
const oneDp = (v) => (v == null ? null : v.toFixed(1));

function stat(season, key) {
  return season?.stats?.[key] ?? null;
}

const PER_GAME_KEY = {
  QB: 'passYdsPerGame',
  RB: 'rushYdsPerGame',
  WR: 'recYdsPerGame',
  TE: 'recYdsPerGame',
};

const TD_KEY = {
  QB: 'passTd',
  RB: 'rushTd',
  WR: 'recTd',
  TE: 'recTd',
};

// value(season, row) returns the RAW numeric (sorting); fmt renders it.
export const COLUMNS = {
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
    { key: 'fg', label: 'FG', value: (s) => stat(s, 'fgMade'), fmt: int, // sorts by makes
      fmtSeason: (s) => (s?.stats ? `${s.stats.fgMade}/${s.stats.fgAtt}` : null) },
    { key: 'fgPct', label: 'PCT', value: (s) => stat(s, 'fgPct'), fmt: oneDp },
    { key: 'fgLong', label: 'LNG', value: (s) => stat(s, 'fgLong'), fmt: int },
    { key: 'xp', label: 'XP', value: (s) => stat(s, 'xpMade'), fmt: int,
      fmtSeason: (s) => (s?.stats ? `${s.stats.xpMade}/${s.stats.xpAtt}` : null) },
  ],
  FLEX: [
    {
      key: 'flexYdsPerGame',
      label: 'Y/G',
      value: (s, row) => stat(s, PER_GAME_KEY[row.position]),
      fmt: oneDp,
    },
    {
      key: 'flexTd',
      label: 'TD',
      value: (s, row) => stat(s, TD_KEY[row.position]),
      fmt: int,
    },
  ],
};

// TE shares WR's shape.
COLUMNS.TE = COLUMNS.WR;

export function columnsFor(slotId, positions) {
  return COLUMNS[slotId === 'FLEX' ? 'FLEX' : positions[0]] ?? [];
}

/** Render one cell: fmtSeason (whole-season formatter, e.g. "12/14")
 *  wins over fmt(value); null season/stat renders an em-dash. */
export function cellText(col, season, row) {
  if (col.fmtSeason) {
    return col.fmtSeason(season) ?? '—';
  }
  const v = col.value(season, row);
  return v == null ? '—' : col.fmt(v);
}
