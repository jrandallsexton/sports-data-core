import { useState, useEffect } from 'react';
import apiWrapper from '../api/apiWrapper';
import { resolveSportLeague } from '../utils/sportLinks';

/**
 * Custom hook to fetch and manage a team's finalized (completed-with-result)
 * games for the embedded MiniSchedule UI on MatchupCards.
 *
 * Calls the slim `/finalized-games` endpoint, which returns games with
 * FinalizedUtc IS NOT NULL, newest-first. When `asOfDate` (ISO 8601) is
 * provided the backend additionally filters to FinalizedUtc <= asOfDate so
 * the MiniSchedule mirrors what the picker could see at pick-time (no future
 * results bleeding into historical week views).
 *
 * @param {string} awaySlug
 * @param {string} homeSlug
 * @param {number} seasonYear
 * @param {string} leagueSport - Backend Sport enum name (e.g. "BaseballMlb")
 * @param {string|null} asOfDate - ISO 8601 timestamp; LeagueWeekMatchupsDto.asOfDate
 *   (= SeasonWeek.EndDate of the displayed week). Omit/null returns the full
 *   set of finalized games for the season (non-pick'em context).
 */
export const useTeamFinalizedGames = (awaySlug, homeSlug, seasonYear, leagueSport, asOfDate = null) => {
  const [showAwayGames, setShowAwayGames] = useState(false);
  const [showHomeGames, setShowHomeGames] = useState(false);
  const [awayGames, setAwayGames] = useState([]);
  const [homeGames, setHomeGames] = useState([]);
  const [awayLoading, setAwayLoading] = useState(false);
  const [homeLoading, setHomeLoading] = useState(false);
  const [awayError, setAwayError] = useState(null);
  const [homeError, setHomeError] = useState(null);

  useEffect(() => {
    // Early-return paths must explicitly clear loading: the race guard below
    // suppresses the in-flight promise's `setAwayLoading(false)` once a newer
    // effect starts, so a transition from "fetching" to "early-return" (e.g.
    // user closes the chevron, leagueSport becomes unmapped) would otherwise
    // strand the spinner.
    if (!showAwayGames || !seasonYear) {
      setAwayLoading(false);
      return;
    }
    const sportLeague = resolveSportLeague(leagueSport);
    if (!sportLeague) {
      setAwayLoading(false);
      return;
    }
    // Race guard: when deps change (e.g. asOfDate flips on week switch) the
    // effect re-runs while the prior fetch may still be in flight. The cleanup
    // function flips `cancelled`, and the resolved promise checks it before
    // calling any setState — so a late response can't stomp newer state.
    let cancelled = false;
    setAwayLoading(true);
    setAwayError(null);
    const { sport, league } = sportLeague;
    apiWrapper.TeamCard.getFinalizedGames(sport, league, awaySlug, seasonYear, asOfDate)
      .then(res => {
        if (cancelled) return;
        setAwayGames(Array.isArray(res.data) ? res.data : []);
      })
      .catch(() => {
        if (cancelled) return;
        setAwayError("Failed to load games");
      })
      .finally(() => {
        if (cancelled) return;
        setAwayLoading(false);
      });
    return () => { cancelled = true; };
  }, [showAwayGames, awaySlug, seasonYear, leagueSport, asOfDate]);

  useEffect(() => {
    // Same loading-reset pattern as the away effect — see comment above.
    if (!showHomeGames || !seasonYear) {
      setHomeLoading(false);
      return;
    }
    const sportLeague = resolveSportLeague(leagueSport);
    if (!sportLeague) {
      setHomeLoading(false);
      return;
    }
    let cancelled = false;
    setHomeLoading(true);
    setHomeError(null);
    const { sport, league } = sportLeague;
    apiWrapper.TeamCard.getFinalizedGames(sport, league, homeSlug, seasonYear, asOfDate)
      .then(res => {
        if (cancelled) return;
        setHomeGames(Array.isArray(res.data) ? res.data : []);
      })
      .catch(() => {
        if (cancelled) return;
        setHomeError("Failed to load games");
      })
      .finally(() => {
        if (cancelled) return;
        setHomeLoading(false);
      });
    return () => { cancelled = true; };
  }, [showHomeGames, homeSlug, seasonYear, leagueSport, asOfDate]);

  return {
    showAwayGames,
    setShowAwayGames,
    showHomeGames,
    setShowHomeGames,
    awayGames,
    homeGames,
    awayLoading,
    homeLoading,
    awayError,
    homeError
  };
};
