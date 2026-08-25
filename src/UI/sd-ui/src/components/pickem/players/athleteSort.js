// Grid ordering for the roster-builder athlete table. Pure so the
// null-handling, tie-breaks, and the season-fallback rule are
// unit-testable.
//
// Sort descriptor: { key: 'name' } or { key: <columnKey>, dir: 'desc'|'asc' }.

export const NAME_SORT = { key: 'name' };

function byName(a, b) {
  return (
    a.lastName.localeCompare(b.lastName) ||
    a.firstName.localeCompare(b.firstName)
  );
}

/**
 * Sort by a stat column via getValue(row, season) -> number|null.
 *
 * Season-fallback rule: order by CURRENT-season values, but when every
 * row's current value is null (week 1, or a slate that hasn't kicked
 * off), fall back to the previous season's values automatically — that
 * is the exact moment last year's numbers ARE the ranking. Rows with a
 * null value under the chosen basis sink to the bottom in both
 * directions (a missing number is never the best or worst matchup), and
 * ties break by name so the order is stable across renders.
 */
export function sortAthletes(rows, sort, getValue) {
  const next = [...rows];

  if (!sort || sort.key === 'name' || typeof getValue !== 'function') {
    next.sort(byName);
    return next;
  }

  const currentOf = (row) => getValue(row, row.currentSeason);
  const useFallback = next.every((row) => currentOf(row) == null);
  const basisOf = useFallback
    ? (row) => getValue(row, row.previousSeason)
    : currentOf;

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
