import { useState, useEffect } from 'react';
import apiWrapper from '../api/apiWrapper';
import { resolveSportLeague } from '../utils/sportLinks';

/**
 * Custom hook to fetch and manage schedule data for both teams in a matchup.
 *
 * Calls the slim `/schedule` endpoint, which returns completed games only,
 * newest-first. When `asOfDate` (ISO 8601) is provided the backend filters
 * games with FinalizedUtc <= asOfDate so the MiniSchedule mirrors what the
 * picker could see at pick-time (no future results bleeding into historical
 * week views).
 *
 * @param {string} awaySlug
 * @param {string} homeSlug
 * @param {number} seasonYear
 * @param {string} leagueSport - Backend Sport enum name (e.g. "BaseballMlb")
 * @param {string|null} asOfDate - ISO 8601 timestamp; LeagueWeekMatchupsDto.asOfDate
 *   (= SeasonWeek.EndDate of the displayed week). Omit/null returns the full
 *   completed schedule (non-pick'em context).
 */
export const useTeamSchedule = (awaySlug, homeSlug, seasonYear, leagueSport, asOfDate = null) => {
  const [showAwayGames, setShowAwayGames] = useState(false);
  const [showHomeGames, setShowHomeGames] = useState(false);
  const [awaySchedule, setAwaySchedule] = useState([]);
  const [homeSchedule, setHomeSchedule] = useState([]);
  const [awayLoading, setAwayLoading] = useState(false);
  const [homeLoading, setHomeLoading] = useState(false);
  const [awayError, setAwayError] = useState(null);
  const [homeError, setHomeError] = useState(null);

  useEffect(() => {
    if (!showAwayGames || !seasonYear) return;
    const sportLeague = resolveSportLeague(leagueSport);
    if (!sportLeague) return;
    setAwayLoading(true);
    setAwayError(null);
    const { sport, league } = sportLeague;
    apiWrapper.TeamCard.getSchedule(sport, league, awaySlug, seasonYear, asOfDate)
      .then(res => {
        setAwaySchedule(Array.isArray(res.data) ? res.data : []);
      })
      .catch(() => setAwayError("Failed to load schedule"))
      .finally(() => setAwayLoading(false));
  }, [showAwayGames, awaySlug, seasonYear, leagueSport, asOfDate]);

  useEffect(() => {
    if (!showHomeGames || !seasonYear) return;
    const sportLeague = resolveSportLeague(leagueSport);
    if (!sportLeague) return;
    setHomeLoading(true);
    setHomeError(null);
    const { sport, league } = sportLeague;
    apiWrapper.TeamCard.getSchedule(sport, league, homeSlug, seasonYear, asOfDate)
      .then(res => {
        setHomeSchedule(Array.isArray(res.data) ? res.data : []);
      })
      .catch(() => setHomeError("Failed to load schedule"))
      .finally(() => setHomeLoading(false));
  }, [showHomeGames, homeSlug, seasonYear, leagueSport, asOfDate]);

  return {
    showAwayGames,
    setShowAwayGames,
    showHomeGames,
    setShowHomeGames,
    awaySchedule,
    homeSchedule,
    awayLoading,
    homeLoading,
    awayError,
    homeError
  };
};
