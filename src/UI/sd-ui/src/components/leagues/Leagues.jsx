// src/components/leagues/Leagues.jsx
import React, { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { FaEye, FaEyeSlash } from "react-icons/fa";
import toast from "react-hot-toast";
import LeaguesApi from "api/leagues/leaguesApi";
import LeagueOverviewCard from "./LeagueOverviewCard";
import CloneLeagueDialog from "./CloneLeagueDialog";
import "./Leagues.css"; // for grid styling

const ALL_LEAGUES = "All";
const ALL_SEASONS = "All";

// Persist the My Leagues filter state so navigating into a league detail and
// back (or returning in a later visit) restores the same view instead of
// resetting to defaults. Stale values are absorbed by the self-healing below.
const FILTERS_STORAGE_KEY = "leagues.filters";

function loadPersistedFilters() {
  let parsed;
  try {
    parsed = JSON.parse(localStorage.getItem(FILTERS_STORAGE_KEY)) || {};
  } catch {
    parsed = {};
  }
  return {
    ...parsed,
    // Coerce to a strict boolean: a stale/legacy non-boolean (e.g. the string
    // "false", which is truthy) must not accidentally enable the toggle.
    showPast: parsed.showPast === true,
  };
}

const Leagues = () => {
  const [leagues, setLeagues] = useState([]);
  const [cloneTarget, setCloneTarget] = useState(null);
  const [cloning, setCloning] = useState(false);
  const [showPast, setShowPast] = useState(() => loadPersistedFilters().showPast);
  const [leagueFilter, setLeagueFilter] = useState(() => loadPersistedFilters().leagueFilter ?? ALL_LEAGUES);
  const [seasonFilter, setSeasonFilter] = useState(() => loadPersistedFilters().seasonFilter ?? ALL_SEASONS);

  // Always fetch past leagues so the toggle is instant rather than a refetch.
  // The user's league count is small, and every row is needed the moment they
  // flip "Past" on.
  const fetchLeagues = useCallback(async () => {
    const result = await LeaguesApi.getUserLeagues({ includeDeactivated: true });
    setLeagues(result);
  }, []);

  useEffect(() => {
    fetchLeagues();
  }, [fetchLeagues]);

  // Persist filters so a round-trip to a league detail (or a later visit)
  // restores the same view.
  useEffect(() => {
    try {
      localStorage.setItem(
        FILTERS_STORAGE_KEY,
        JSON.stringify({ showPast, leagueFilter, seasonFilter })
      );
    } catch {
      /* ignore storage failures (private mode, quota) */
    }
  }, [showPast, leagueFilter, seasonFilter]);

  const handleClone = async (name, inviteMembers) => {
    if (!cloneTarget) return;
    setCloning(true);
    try {
      await LeaguesApi.cloneLeague(cloneTarget.id, { name, inviteMembers });
      toast.success("League duplicated!");
      setCloneTarget(null);
      await fetchLeagues();
    } catch (err) {
      console.error("Failed to clone league:", err);
      toast.error("Failed to duplicate league. Please try again.");
    } finally {
      setCloning(false);
    }
  };

  // Everything below is derived from `leagues` rather than mirrored into state,
  // so the filters can't drift out of sync with a refetch.
  const hasPast = leagues.some((l) => l.deactivatedUtc);
  const scoped = showPast ? leagues : leagues.filter((l) => !l.deactivatedUtc);

  const availableLeagues = [
    ...new Set(scoped.map((l) => l.league).filter(Boolean)),
  ].sort();

  // Self-healing: if the selected league leaves scope (e.g. it only existed
  // among past leagues and "Past" was switched off), fall back to All instead
  // of stranding the user on an empty grid.
  const activeFilter = availableLeagues.includes(leagueFilter)
    ? leagueFilter
    : ALL_LEAGUES;

  // Season years present in the current scope, newest-first. Derived from
  // `scoped` (post-"Past" toggle) so picking a season can't strand the user on a
  // year that isn't currently shown; the self-heal falls back to All otherwise.
  const availableSeasons = [
    ...new Set(scoped.map((l) => l.seasonYear).filter(Boolean)),
  ].sort((a, b) => b - a);

  const activeSeasonFilter = availableSeasons.includes(seasonFilter)
    ? seasonFilter
    : ALL_SEASONS;

  const visibleLeagues = scoped.filter(
    (l) =>
      (activeFilter === ALL_LEAGUES || l.league === activeFilter) &&
      (activeSeasonFilter === ALL_SEASONS || l.seasonYear === activeSeasonFilter)
  );

  const showFilterBar =
    hasPast || availableLeagues.length > 1 || availableSeasons.length > 1;

  return (
    <div className="page-container">
      <div className="leagues-header">
        <h1>My Leagues</h1>
        <Link to="/app/league/create" className="create-league-button">
          + Create League
        </Link>
      </div>

      {leagues.length > 0 && showFilterBar && (
        <div className="leagues-filter-bar">
          {availableSeasons.length > 1 && (
            <div className="league-filter-chips" role="group" aria-label="Filter by season">
              {[ALL_SEASONS, ...availableSeasons].map((option) => (
                <button
                  key={option}
                  type="button"
                  className={`league-filter-chip${
                    activeSeasonFilter === option ? " active" : ""
                  }`}
                  onClick={() => setSeasonFilter(option)}
                  aria-pressed={activeSeasonFilter === option}
                >
                  {option}
                </button>
              ))}
            </div>
          )}
          {availableLeagues.length > 1 && (
            <div className="league-filter-chips" role="group" aria-label="Filter by league">
              {[ALL_LEAGUES, ...availableLeagues].map((option) => (
                <button
                  key={option}
                  type="button"
                  className={`league-filter-chip${
                    activeFilter === option ? " active" : ""
                  }`}
                  onClick={() => setLeagueFilter(option)}
                  aria-pressed={activeFilter === option}
                >
                  {option}
                </button>
              ))}
            </div>
          )}
          {hasPast && (
            <button
              type="button"
              className={`past-leagues-toggle${showPast ? " active" : ""}`}
              onClick={() => setShowPast(!showPast)}
              title={showPast ? "Hide past leagues" : "Show past leagues"}
              aria-pressed={showPast}
            >
              {showPast ? <FaEye /> : <FaEyeSlash />} Past
            </button>
          )}
        </div>
      )}

      {leagues.length === 0 ? (
        <p>
          You’re not part of any leagues yet.{" "}
          <Link to="/app/league/create">Create one</Link>.
        </p>
      ) : visibleLeagues.length === 0 ? (
        <p>No leagues match this filter.</p>
      ) : (
        <div className="league-grid">
          {visibleLeagues.map((league) => (
            <LeagueOverviewCard
              key={league.id}
              league={league}
              onDuplicate={setCloneTarget}
            />
          ))}
        </div>
      )}

      {cloneTarget && (
        <CloneLeagueDialog
          league={cloneTarget}
          submitting={cloning}
          onClose={() => {
            if (!cloning) setCloneTarget(null);
          }}
          onConfirm={handleClone}
        />
      )}
    </div>
  );
};

export default Leagues;
