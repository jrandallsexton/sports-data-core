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

const Leagues = () => {
  const [leagues, setLeagues] = useState([]);
  const [cloneTarget, setCloneTarget] = useState(null);
  const [cloning, setCloning] = useState(false);
  const [showPast, setShowPast] = useState(false);
  const [leagueFilter, setLeagueFilter] = useState(ALL_LEAGUES);

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

  const visibleLeagues =
    activeFilter === ALL_LEAGUES
      ? scoped
      : scoped.filter((l) => l.league === activeFilter);

  const showFilterBar = hasPast || availableLeagues.length > 1;

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
