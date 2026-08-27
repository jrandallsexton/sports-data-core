import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useContestUpdates } from '../../../contexts/ContestUpdatesContext';
import PlayerPickemApi from '../../../api/playerPickemApi';
import LeaguesApi from '../../../api/leagues/leaguesApi';
import {
  SLOT_DEFS,
  eligiblePositions,
  canAssign,
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

// NCAAFB is the product; NFL rides along for closed-testing coverage
// (and who knows). `sport` matches LeagueSummaryDto.Sport for resolving
// which of the user's Player-Pick'em-enabled leagues this toggle targets.
const LEAGUES = [
  { id: 'ncaa', label: 'NCAAFB (FBS)', sport: 'FootballNcaa' },
  { id: 'nfl', label: 'NFL', sport: 'FootballNfl' },
];

// Fixed to opening week for now; a week selector (and
// deriving the current week server-side) is future work. Exported so
// LeaguePicksRouter can canonicalize the URL to the week this page
// actually renders.
export const SEASON_YEAR = 2026;
export const WEEK = 1;

// Full-depth FBS position lists run to ~2,000 rows (WR); the grid pages
// client-side — the payload is already in the browser, so filtering and
// paging never refetch. 10 keeps the page scannable (each athlete is a
// two-line row pair, so 10 athletes ≈ 20 visual rows); operator may tune
// toward 15.
const PAGE_SIZE = 10;

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

/** Server lineup → the {slotId: slot} shape the slot row renders. */
function rosterFromLineup(lineup) {
  return Object.fromEntries((lineup?.slots ?? []).map((s) => [s.slotId, s]));
}

/** First useful message out of an API validation failure, else a fallback. */
function errorMessage(err, fallback) {
  const errors = err?.response?.data?.errors;
  if (Array.isArray(errors) && errors.length > 0 && errors[0]?.errorMessage) {
    return errors[0].errorMessage;
  }
  return fallback;
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
 * has current data (week 1). The roster persists server-side to the
 * user's PlayerPickem-type league for the toggled sport, with per-player
 * derived locking (kickoff−5). NCAAFB is the product; the NFL toggle
 * exists for closed-testing coverage.
 */
function PlayerRosterBuilder() {
  // Optional league scope from the route (league cards pass their id) —
  // without it the page falls back to the first PlayerPickem league per
  // sport.
  const { leagueId: routeLeagueId, week: routeWeekParam, phase: routePhaseParam } = useParams();
  const [league, setLeague] = useState('ncaa');
  // Week identity comes from the ROUTE, which LeaguePicksRouter has
  // already canonicalized to the league's current phase-qualified week
  // (a preseason-only league lives at its preseason week). The pinned
  // constants remain only as a fallback for the league-less admin view.
  const routeWeekNum = Number(routeWeekParam);
  const seasonWeek = Number.isInteger(routeWeekNum) && routeWeekNum > 0 ? routeWeekNum : WEEK;
  const seasonPhase = routePhaseParam ?? 'regular';
  const [roster, setRoster] = useState({});
  // Live lineup total from the server's read-time scoring (matrix-priced
  // statlines, refreshed with the play-driven stat pipeline). Null until
  // any anchored slot has a statline.
  const [totalPoints, setTotalPoints] = useState(null);
  // Live-refresh tickle: bumping this re-runs the lineup fetch WITHOUT
  // the loading/reset churn of a league change (see the effect below).
  const [refreshTick, setRefreshTick] = useState(0);
  // The user's PlayerPickem-type leagues (null = still loading). The
  // SPORT isn't a free choice — it's a fact of these leagues: the page
  // auto-selects the first sport with a player league, and the toggle
  // only renders when leagues span more than one sport.
  const [playerLeagues, setPlayerLeagues] = useState(null);
  // The PickemGroup this roster persists to for the selected sport.
  const [pickemLeague, setPickemLeague] = useState(null);
  const [rosterLoading, setRosterLoading] = useState(true);
  const [saveError, setSaveError] = useState(null);
  const [activeSlotId, setActiveSlotId] = useState('QB');
  const [athletes, setAthletes] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [sort, setSort] = useState(NAME_SORT);
  const [filterText, setFilterText] = useState('');
  const [opponentText, setOpponentText] = useState('');
  const [page, setPage] = useState(0);

  // Load the user's PlayerPickem leagues once; auto-select the first
  // sport that actually has one.
  useEffect(() => {
    let ignore = false;
    LeaguesApi.getUserLeagues()
      .then((leagues) => {
        if (ignore) return;
        const mine = (leagues ?? []).filter((l) => l.groupType === 'PlayerPickem');
        setPlayerLeagues(mine);
        // Route-scoped league wins; its sport drives the page. Fallback:
        // first sport that has a player league.
        const routed = routeLeagueId
          ? mine.find((l) => l.id === routeLeagueId)
          : null;
        const target = routed
          ? LEAGUES.find((opt) => opt.sport === routed.sport)
          : LEAGUES.find((opt) => mine.some((l) => l.sport === opt.sport));
        if (target) setLeague(target.id);
      })
      .catch(() => {
        if (!ignore) {
          setPlayerLeagues([]);
          setSaveError('Could not load your leagues.');
        }
      });
    return () => {
      ignore = true;
    };
  }, [routeLeagueId]);

  // Resolve the target league for the selected sport, then load the
  // server lineup (whose first read of a new week performs the lazy
  // carry-over clone server-side).
  const lastRefreshTickRef = useRef(0);
  useEffect(() => {
    if (playerLeagues === null) return undefined; // leagues still loading

    let ignore = false;
    // A live-refresh rerun (refreshTick bumped) keeps the current roster
    // on screen and silently swaps in fresh points; only a real
    // league/week change resets the surface.
    const isLiveRefresh = refreshTick !== lastRefreshTickRef.current;
    lastRefreshTickRef.current = refreshTick;
    if (!isLiveRefresh) {
      setRosterLoading(true);
      setSaveError(null);
      setRoster({});
      setTotalPoints(null); // never show a total for a roster we cleared
    }

    const sport = LEAGUES.find((l) => l.id === league)?.sport;
    const routed = routeLeagueId
      ? playerLeagues.find((l) => l.id === routeLeagueId && l.sport === sport)
      : null;
    const target = routed ?? playerLeagues.find((l) => l.sport === sport) ?? null;
    setPickemLeague(target);

    if (!target) {
      setRosterLoading(false);
      return undefined;
    }

    PlayerPickemApi.getMyLineup(target.id, target.seasonYear ?? SEASON_YEAR, seasonWeek)
      .then((response) => {
        if (ignore) return;
        setRoster(rosterFromLineup(response.data));
        setTotalPoints(response.data?.totalPoints ?? null);
      })
      .catch(() => {
        if (!ignore) setSaveError('Could not load your roster.');
      })
      .finally(() => {
        if (!ignore) setRosterLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [league, playerLeagues, routeLeagueId, seasonWeek, refreshTick]);

  // ── Live scoring refresh (Phase 1 — see scoring.md) ─────────────────
  // The play-completed SignalR events already flowing into
  // ContestUpdatesContext stamp contests[id].lastUpdated. When activity
  // lands on a contest one of OUR slots is anchored to, refetch the
  // lineup twice: once shortly after the play (fast feedback) and once
  // after the Producer stat-document debounce window (~3 min) so the
  // numbers catch up. No polling: quiet games cost zero requests.
  const { contests: liveContests } = useContestUpdates();
  const anchoredActivity = useMemo(
    () =>
      Object.values(roster)
        .filter((slot) => slot?.contestId)
        .reduce(
          (latest, slot) =>
            Math.max(latest, liveContests[slot.contestId]?.lastUpdated ?? 0),
          0
        ),
    [roster, liveContests]
  );
  const refreshTimersRef = useRef([]);
  useEffect(() => {
    if (anchoredActivity === 0) return undefined;
    if (refreshTimersRef.current.length > 0) return undefined; // pair already pending

    const bump = () => setRefreshTick((t) => t + 1);
    refreshTimersRef.current = [
      setTimeout(bump, 45 * 1000),
      setTimeout(() => {
        bump();
        refreshTimersRef.current = [];
      }, 240 * 1000),
    ];
    return undefined;
  }, [anchoredActivity]);
  useEffect(
    () => () => {
      refreshTimersRef.current.forEach(clearTimeout);
      refreshTimersRef.current = [];
    },
    []
  );

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
        PlayerPickemApi.getAthletesByPosition(
          'football', league, pos,
          pickemLeague?.seasonYear ?? SEASON_YEAR, seasonWeek, seasonPhase
        )
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
  }, [positions, league, pickemLeague, seasonWeek, seasonPhase]);

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

  const handleAssign = async (athlete) => {
    // Client-side pre-checks (eligibility, duplicates) fail fast; the
    // server re-validates everything INCLUDING locks, which only it can
    // judge authoritatively.
    if (!pickemLeague || !canAssign(roster, activeSlotId, athlete)) return;
    setSaveError(null);
    try {
      const response = await PlayerPickemApi.upsertSlot(
        pickemLeague.id, pickemLeague.seasonYear ?? SEASON_YEAR, seasonWeek, activeSlotId, athlete
      );
      setRoster((prev) => ({ ...prev, [activeSlotId]: response.data }));
      // Slot points and the total must come from one server response —
      // pull a silent refresh so they can't drift.
      setRefreshTick((t) => t + 1);
    } catch (err) {
      setSaveError(errorMessage(err, 'Could not save that pick.'));
    }
  };

  const handleRemove = async (slotId) => {
    if (!pickemLeague) return;
    setSaveError(null);
    try {
      await PlayerPickemApi.clearSlot(pickemLeague.id, pickemLeague.seasonYear ?? SEASON_YEAR, seasonWeek, slotId);
      setRoster((prev) => {
        const next = { ...prev };
        delete next[slotId];
        return next;
      });
      setRefreshTick((t) => t + 1); // re-sync total with the server

    } catch (err) {
      setSaveError(errorMessage(err, 'Could not clear that slot.'));
    }
  };

  const activeSlot = SLOT_DEFS.find((s) => s.id === activeSlotId);
  const activeOccupantLocked = roster[activeSlotId]?.isLocked === true;
  const oppDefLabel =
    OPP_DEF_LABEL[activeSlot?.id === 'FLEX' ? 'FLEX' : positions[0]];

  return (
    <div className="roster-builder">
      <h2 className="roster-builder-title">Player Pick&rsquo;em Roster</h2>
      <p className="roster-builder-sub">
        {seasonPhase === 'preseason' ? 'Preseason ' : seasonPhase === 'postseason' ? 'Postseason ' : ''}Week {seasonWeek} &middot; {pickemLeague?.seasonYear ?? SEASON_YEAR} &middot;{' '}
        {LEAGUES.find((l) => l.id === league)?.label}
        {pickemLeague ? (
          <> &middot; <strong>{pickemLeague.name}</strong></>
        ) : null}
        {totalPoints != null && totalPoints !== 0 ? (
          <> &middot; <strong className="roster-total-points">{totalPoints.toFixed(1)} pts</strong></>
        ) : null}
      </p>

      {/* The sport is a fact of the user's player leagues, not a free
          choice — the toggle only exists when their leagues span more
          than one sport. */}
      {(() => {
        const available = LEAGUES.filter((opt) =>
          (playerLeagues ?? []).some((l) => l.sport === opt.sport)
        );
        return available.length > 1 ? (
          <div className="roster-leagues" role="group" aria-label="League">
            {available.map((l) => (
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
        ) : null;
      })()}

      {rosterLoading ? (
        <div className="roster-grid-status">Loading your roster&hellip;</div>
      ) : playerLeagues !== null && playerLeagues.length === 0 ? (
        <div className="roster-grid-status roster-grid-status--error">
          You&rsquo;re not in a Player Pick&rsquo;em league yet &mdash; the
          grid is browsable, but picks can&rsquo;t be saved.
        </div>
      ) : null}
      {saveError ? (
        <div className="roster-grid-status roster-grid-status--error" role="alert">
          {saveError}
        </div>
      ) : null}

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
                    ? `${filled.isLocked ? '🔒 ' : ''}${filled.firstName.charAt(0)}. ${filled.lastName}`
                    : slot.disabled
                      ? 'Soon'
                      : '—'}
                </span>
                {filled && filled.points != null ? (
                  <span className="roster-slot-points" title={filled.statLine || undefined}>
                    {filled.points.toFixed(1)}
                  </span>
                ) : null}
              </button>
              {/* Locked slots hide the remove affordance — the server
                  would reject it anyway; don't offer a dead button. */}
              {filled && !filled.isLocked ? (
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
                        disabled={rostered || !pickemLeague || activeOccupantLocked}
                        title={
                          activeOccupantLocked
                            ? 'This slot is locked — its game has started.'
                            : !pickemLeague
                              ? 'No Player Pick’em league to save to.'
                              : undefined
                        }
                        onClick={() => handleAssign(a)}
                      >
                        {rostered
                          ? 'Rostered'
                          : activeOccupantLocked
                            ? 'Locked'
                            : addLabel}
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
