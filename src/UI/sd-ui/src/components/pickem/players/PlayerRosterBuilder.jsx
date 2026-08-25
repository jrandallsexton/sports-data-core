import { useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import PlayerPickemApi from '../../../api/playerPickemApi';
import {
  SLOT_DEFS,
  slotById,
  eligiblePositions,
  assign,
  remove,
  isRostered,
} from './rosterLogic';
import {
  NAME_SORT,
  sortAthletes,
  filterAthletes,
  filterByOpponent,
} from './athleteSort';
import { columnsFor, cellText } from './gridColumns';
import './PlayerRosterBuilder.css';

// Selections survive reloads — rudimentary stand-in for the carry-over
// behavior until PlayerLineup entities exist server-side. Keyed per
// league so an NCAAFB draft never bleeds into the NFL view.
const rosterKey = (league) => `playerPickemRosterDraft.${league}`;

// NCAAFB is the product; NFL rides along for closed-testing coverage
// (and who knows).
const LEAGUES = [
  { id: 'ncaa', label: 'NCAAFB (FBS)' },
  { id: 'nfl', label: 'NFL' },
];

// Fixed to opening week for the admin preview; a week selector (and
// deriving the current week server-side) is future work.
const SEASON_YEAR = 2026;
const WEEK = 1;

// Full-depth FBS position lists run to ~2,000 rows (WR); the grid pages
// client-side — the payload is already in the browser, so filtering and
// paging never refetch.
const PAGE_SIZE = 25;

// Meaning of opponentDefPerGame varies by the position being browsed.
// FLEX merges positions, so it gets the generic label and the per-row
// position badge carries the meaning.
const OPP_DEF_LABEL = {
  QB: 'Opp Pass Alw/G',
  RB: 'Opp Rush Alw/G',
  WR: 'Opp Pass Alw/G',
  TE: 'Opp Pass Alw/G',
  K: 'Opp Pts Alw/G',
  FLEX: 'Opp Def/G',
};

// Sort key for the opponent-defense column — a row-level number, not a
// season stat, so its getValue ignores the season block (the current/
// previous fallback in sortAthletes never distinguishes for it).
const OPP_DEF_SORT_KEY = 'oppDef';

/**
 * A stored draft is untrusted input: JSON.parse happily returns null,
 * arrays, or strings (all valid JSON). Accept only a plain object whose
 * keys are real slot ids and whose values look like athletes; anything
 * else degrades to the slot-by-slot best effort or an empty roster.
 * Mirrors the mobile screen's sanitizer.
 */
function sanitizeRoster(raw) {
  try {
    const parsed = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
      return {};
    }
    const next = {};
    for (const [slotId, val] of Object.entries(parsed)) {
      if (
        slotById(slotId) &&
        val !== null &&
        typeof val === 'object' &&
        !Array.isArray(val) &&
        typeof val.athleteId === 'string' &&
        // The slot chip renders firstName.charAt(0) + lastName — an entry
        // missing either would crash on first paint.
        typeof val.firstName === 'string' &&
        typeof val.lastName === 'string'
      ) {
        next[slotId] = val;
      }
    }
    return next;
  } catch {
    return {};
  }
}

/**
 * Player Pick'em roster builder (admin-gated, v1 exploration).
 *
 * Teaser-style slot row up top (fixed v1 shape, DEF disabled); selecting
 * a slot loads the athlete grid below filtered to that slot's eligible
 * positions. Each athlete renders as a row PAIR in shared stat columns:
 * current season on the primary row, previous season directly beneath in
 * the same columns for vertical comparison. Sorting orders the pairs by
 * the current-season value, falling back to previous-season when no row
 * has current data (week 1). Roster is local-only (localStorage), keyed
 * per league. NCAAFB is the product; the NFL toggle exists for
 * closed-testing coverage.
 */
function PlayerRosterBuilder() {
  const [league, setLeague] = useState('ncaa');
  const [roster, setRoster] = useState(() =>
    sanitizeRoster(localStorage.getItem(rosterKey('ncaa')) ?? 'null')
  );
  // Which league the current roster state belongs to. Guards the save
  // effect during a league switch — without it, the old league's roster
  // would be written over the new league's stored draft before the load
  // effect has swapped it in.
  const rosterLeagueRef = useRef('ncaa');
  const [activeSlotId, setActiveSlotId] = useState('QB');
  const [athletes, setAthletes] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [sort, setSort] = useState(NAME_SORT);
  const [filterText, setFilterText] = useState('');
  const [opponentText, setOpponentText] = useState('');
  const [page, setPage] = useState(0);

  // Load the new league's draft on switch.
  useEffect(() => {
    if (rosterLeagueRef.current === league) return;
    setRoster(sanitizeRoster(localStorage.getItem(rosterKey(league)) ?? 'null'));
    rosterLeagueRef.current = league;
  }, [league]);

  // Save under the league the roster BELONGS to (the ref), never the
  // currently selected league. During a switch the two disagree for one
  // render while state is still the old league's lineup; keying the
  // write by the ref makes effect ordering irrelevant — the switch pass
  // doesn't run this effect at all (roster hasn't changed), and once the
  // loaded roster commits, ref and league agree again.
  useEffect(() => {
    localStorage.setItem(rosterKey(rosterLeagueRef.current), JSON.stringify(roster));
  }, [roster]);

  const positions = useMemo(
    () => eligiblePositions(activeSlotId),
    [activeSlotId]
  );

  const columns = useMemo(
    () => columnsFor(activeSlotId, positions),
    [activeSlotId, positions]
  );

  // Stat columns differ per slot — a sort on CMP% means nothing on the
  // RB grid, so slot and league changes reset sort, filter, and page
  // together.
  useEffect(() => {
    setSort(NAME_SORT);
    setFilterText('');
    setOpponentText('');
    setPage(0);
  }, [activeSlotId, league]);

  // Any change to what's shown restarts at the first page — a filter or
  // re-sort that leaves you stranded on page 40 of a smaller result set
  // reads as an empty grid.
  useEffect(() => {
    setPage(0);
  }, [filterText, opponentText, sort]);

  useEffect(() => {
    if (positions.length === 0) return undefined;

    let ignore = false;
    setLoading(true);
    setError(null);

    Promise.all(
      positions.map((pos) =>
        PlayerPickemApi.getAthletesByPosition('football', league, pos, SEASON_YEAR, WEEK)
      )
    )
      .then((responses) => {
        if (ignore) return;
        // Raw rows only — display order is derived from the sort state so
        // toggling a header doesn't refetch.
        setAthletes(responses.flatMap((r) => r.data.athletes));
      })
      .catch(() => {
        if (!ignore) setError('Could not load athletes.');
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [positions, league]);

  const activeCol = columns.find((c) => c.key === sort.key);
  const getSortValue = useMemo(() => {
    if (sort.key === OPP_DEF_SORT_KEY) {
      return (row) => row.opponentDefPerGame;
    }
    if (!activeCol) return undefined;
    return (row, season) => activeCol.value(season, row);
  }, [sort.key, activeCol]);

  const sortedAthletes = useMemo(
    () =>
      sortAthletes(
        filterByOpponent(filterAthletes(athletes, filterText), opponentText),
        sort,
        getSortValue
      ),
    [athletes, filterText, opponentText, sort, getSortValue]
  );

  const pageCount = Math.max(1, Math.ceil(sortedAthletes.length / PAGE_SIZE));
  const pagedAthletes = useMemo(
    () => sortedAthletes.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE),
    [sortedAthletes, page]
  );

  // First click on a stat header = descending (big numbers are what a
  // picker hunts); second click flips; other headers switch columns.
  const toggleSort = (key) => {
    setSort((prev) =>
      prev.key === key
        ? { key, dir: prev.dir === 'desc' ? 'asc' : 'desc' }
        : { key, dir: 'desc' }
    );
  };

  const sortIndicator = (key) =>
    sort.key === key ? (sort.dir === 'asc' ? ' ▲' : ' ▼') : '';

  const ariaSort = (key) =>
    sort.key === key
      ? sort.dir === 'asc'
        ? 'ascending'
        : 'descending'
      : undefined;

  const handleAssign = (athlete) => {
    setRoster((prev) => assign(prev, activeSlotId, athlete));
  };

  const handleRemove = (slotId) => {
    setRoster((prev) => remove(prev, slotId));
  };

  const activeSlot = SLOT_DEFS.find((s) => s.id === activeSlotId);
  const oppDefLabel =
    OPP_DEF_LABEL[activeSlot?.id === 'FLEX' ? 'FLEX' : positions[0]];

  return (
    <div className="roster-builder">
      <h2 className="roster-builder-title">Player Pick&rsquo;em Roster</h2>
      <p className="roster-builder-sub">
        Week {WEEK} &middot; {SEASON_YEAR} &middot;{' '}
        {LEAGUES.find((l) => l.id === league)?.label} &mdash; admin preview,
        selections are local-only
      </p>

      <div className="roster-leagues" role="group" aria-label="League">
        {LEAGUES.map((l) => (
          <button
            key={l.id}
            type="button"
            className={`roster-league-btn${l.id === league ? ' roster-league-btn--active' : ''}`}
            aria-pressed={l.id === league}
            onClick={() => setLeague(l.id)}
          >
            {l.label}
          </button>
        ))}
      </div>

      {/* Button group, not tabs: there's no tabpanel relationship here and
          the remove buttons live between the slot controls, so tablist
          semantics would promise keyboard behavior this doesn't have. */}
      <div className="roster-slots" role="group" aria-label="Lineup slots">
        {SLOT_DEFS.map((slot) => {
          const filled = roster[slot.id];
          const isActive = slot.id === activeSlotId;
          return (
            <div
              key={slot.id}
              className={[
                'roster-slot',
                filled ? 'roster-slot--filled' : '',
                isActive ? 'roster-slot--active' : '',
                slot.disabled ? 'roster-slot--disabled' : '',
              ]
                .filter(Boolean)
                .join(' ')}
            >
              <button
                type="button"
                aria-pressed={isActive}
                className="roster-slot-btn"
                disabled={slot.disabled}
                title={slot.disabled ? 'Team defense — coming soon' : undefined}
                onClick={() => setActiveSlotId(slot.id)}
              >
                <span className="roster-slot-label">{slot.label}</span>
                <span className="roster-slot-player">
                  {filled
                    ? `${filled.firstName.charAt(0)}. ${filled.lastName}`
                    : slot.disabled
                      ? 'Soon'
                      : '—'}
                </span>
              </button>
              {filled ? (
                <button
                  type="button"
                  className="roster-slot-remove"
                  aria-label={`Remove ${filled.firstName} ${filled.lastName}`}
                  onClick={() => handleRemove(slot.id)}
                >
                  &times;
                </button>
              ) : null}
            </div>
          );
        })}
      </div>

      <div className="roster-toolbar">
        <input
          type="search"
          className="roster-filter"
          placeholder="Filter by player or team…"
          aria-label="Filter athletes by name or team"
          value={filterText}
          onChange={(e) => setFilterText(e.target.value)}
        />
        <span className="roster-toolbar-count">
          {sortedAthletes.length.toLocaleString()} athletes
        </span>
        {/* Right-aligned so it sits over the opponent columns — the
            matchup hunt: "UMass is horrible, show me the RBs playing
            them this weekend." */}
        <input
          type="search"
          className="roster-filter roster-filter--opponent"
          placeholder="Filter by opponent…"
          aria-label="Filter athletes by week opponent"
          value={opponentText}
          onChange={(e) => setOpponentText(e.target.value)}
        />
      </div>

      {loading ? (
        <div className="roster-grid-status">Loading athletes&hellip;</div>
      ) : error ? (
        <div className="roster-grid-status roster-grid-status--error">
          {error}
        </div>
      ) : (
        <div className="roster-grid-wrap">
          <table className="roster-grid">
            <thead>
              <tr>
                <th aria-sort={sort.key === 'name' ? 'ascending' : undefined}>
                  <button
                    type="button"
                    className="roster-grid-sort"
                    onClick={() => setSort(NAME_SORT)}
                  >
                    Player{sort.key === 'name' ? ' ▲' : ''}
                  </button>
                </th>
                {columns.map((col) => (
                  <th
                    key={col.key}
                    className="roster-grid-num"
                    aria-sort={ariaSort(col.key)}
                  >
                    <button
                      type="button"
                      className="roster-grid-sort"
                      onClick={() => toggleSort(col.key)}
                    >
                      {col.label}
                      {sortIndicator(col.key)}
                    </button>
                  </th>
                ))}
                <th>Opponent</th>
                {/* No opponent-defense SORT on FLEX: the value is rush yds
                    allowed/G for an RB but pass yds allowed/G for a WR/TE —
                    different units, a cross-position ranking would lie. The
                    column still displays; the per-row position badge carries
                    each number's meaning. */}
                {activeSlot?.id === 'FLEX' ? (
                  <th className="roster-grid-num">{oppDefLabel}</th>
                ) : (
                  <th
                    className="roster-grid-num"
                    aria-sort={ariaSort(OPP_DEF_SORT_KEY)}
                  >
                    <button
                      type="button"
                      className="roster-grid-sort"
                      onClick={() => toggleSort(OPP_DEF_SORT_KEY)}
                    >
                      {oppDefLabel}
                      {sortIndicator(OPP_DEF_SORT_KEY)}
                    </button>
                  </th>
                )}
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {pagedAthletes.map((a) => {
                const rostered = isRostered(roster, a.athleteId);
                // Adding into an occupied slot replaces its player — say so
                // on the button instead of springing it on the user.
                const occupant = roster[activeSlotId];
                const addLabel =
                  occupant && occupant.athleteId !== a.athleteId
                    ? `Replace ${occupant.firstName.charAt(0)}. ${occupant.lastName}`
                    : 'Add';
                return [
                  <tr key={a.athleteId} className="roster-grid-row">
                    <td className="roster-grid-player">
                      <span className="roster-grid-name">
                        {a.lastName}, {a.firstName}
                        {activeSlot?.id === 'FLEX' ? (
                          <span className="roster-grid-pos">{a.position}</span>
                        ) : null}
                      </span>
                      <Link
                        className="roster-grid-team"
                        to={`/sport/football/${league}/team/${a.teamSlug}`}
                      >
                        {a.teamName}
                      </Link>
                    </td>
                    {columns.map((col) => (
                      <td key={col.key} className="roster-grid-num">
                        {cellText(col, a.currentSeason, a)}
                      </td>
                    ))}
                    <td>
                      {a.opponentName ? (
                        a.opponentSlug ? (
                          <Link to={`/sport/football/${league}/team/${a.opponentSlug}`}>
                            {a.opponentName}
                          </Link>
                        ) : (
                          a.opponentName
                        )
                      ) : (
                        'BYE'
                      )}
                    </td>
                    <td className="roster-grid-num">
                      {a.opponentDefPerGame?.toFixed(1) ?? '—'}
                    </td>
                    <td className="roster-grid-action">
                      <button
                        type="button"
                        className="roster-grid-add"
                        disabled={rostered}
                        onClick={() => handleAssign(a)}
                      >
                        {rostered ? 'Rostered' : addLabel}
                      </button>
                    </td>
                  </tr>,
                  <tr key={`${a.athleteId}-prev`} className="roster-grid-subrow">
                    <td>
                      {a.previousSeason
                        ? `${a.previousSeason.seasonYear} · ${a.previousSeason.gamesPlayed} G`
                        : 'No prior season'}
                    </td>
                    {columns.map((col) => (
                      <td key={col.key} className="roster-grid-num">
                        {a.previousSeason
                          ? cellText(col, a.previousSeason, a)
                          : '—'}
                      </td>
                    ))}
                    <td colSpan={3} />
                  </tr>,
                ];
              })}
              {sortedAthletes.length === 0 ? (
                <tr>
                  <td colSpan={columns.length + 4} className="roster-grid-status">
                    {filterText || opponentText
                      ? 'No athletes match the filters.'
                      : 'No athletes for this position.'}
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
          {pageCount > 1 ? (
            <div className="roster-pager">
              <button
                type="button"
                className="roster-pager-btn"
                disabled={page === 0}
                onClick={() => setPage((p) => Math.max(0, p - 1))}
              >
                &lsaquo; Prev
              </button>
              <span className="roster-pager-label">
                Page {page + 1} of {pageCount}
              </span>
              <button
                type="button"
                className="roster-pager-btn"
                disabled={page >= pageCount - 1}
                onClick={() => setPage((p) => Math.min(pageCount - 1, p + 1))}
              >
                Next &rsaquo;
              </button>
            </div>
          ) : null}
        </div>
      )}
    </div>
  );
}

export default PlayerRosterBuilder;
