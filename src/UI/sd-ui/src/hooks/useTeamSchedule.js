import { useState, useEffect } from 'react';
import apiWrapper from '../api/apiWrapper';

/**
 * Custom hook to fetch and manage schedule data for both teams in a matchup
 * @param {string} awaySlug - Away team slug identifier
 * @param {string} homeSlug - Home team slug identifier
 * @param {number} seasonYear - Season year
 * @returns {object} Schedule state and handlers for both teams
 */
export const useTeamSchedule = (awaySlug, homeSlug, seasonYear) => {
  const [showAwayGames, setShowAwayGames] = useState(false);
  const [showHomeGames, setShowHomeGames] = useState(false);
  const [awaySchedule, setAwaySchedule] = useState([]);
  const [homeSchedule, setHomeSchedule] = useState([]);
  const [awayLoading, setAwayLoading] = useState(false);
  const [homeLoading, setHomeLoading] = useState(false);
  const [awayError, setAwayError] = useState(null);
  const [homeError, setHomeError] = useState(null);

  // Fetch away team schedule
  useEffect(() => {
    if (!showAwayGames) return;
    setAwayLoading(true);
    setAwayError(null);
    apiWrapper.TeamCard.getBySlugAndSeason(awaySlug, seasonYear)
      .then(res => {
        setAwaySchedule(Array.isArray(res.data?.schedule) ? res.data.schedule : []);
      })
      .catch(() => setAwayError("Failed to load schedule"))
      .finally(() => setAwayLoading(false));
  }, [showAwayGames, awaySlug, seasonYear]);

  // Fetch home team schedule
  useEffect(() => {
    if (!showHomeGames) return;
    setHomeLoading(true);
    setHomeError(null);
    apiWrapper.TeamCard.getBySlugAndSeason(homeSlug, seasonYear)
      .then(res => {
        setHomeSchedule(Array.isArray(res.data?.schedule) ? res.data.schedule : []);
      })
      .catch(() => setHomeError("Failed to load schedule"))
      .finally(() => setHomeLoading(false));
  }, [showHomeGames, homeSlug, seasonYear]);

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
