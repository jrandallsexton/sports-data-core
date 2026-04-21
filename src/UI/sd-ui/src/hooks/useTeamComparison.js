import { useState } from 'react';
import apiWrapper from '../api/apiWrapper';
import { resolveSportLeague } from '../utils/sportLinks';

/**
 * Custom hook to manage team comparison dialog state and data fetching
 * @param {object} matchup - Matchup data object
 * @param {number} seasonYear - Season year for the comparison data
 * @param {string} leagueSport - Backend Sport enum name (e.g. "BaseballMlb") used to
 *   resolve the {sport, league} URL segments for the TeamCard API
 * @returns {object} { showComparison, comparisonLoading, comparisonData, handleOpenComparison, handleCloseComparison }
 */
export const useTeamComparison = (matchup, seasonYear, leagueSport) => {
  const [showComparison, setShowComparison] = useState(false);
  const [comparisonLoading, setComparisonLoading] = useState(false);
  const [comparisonData, setComparisonData] = useState(null);

  const handleOpenComparison = async () => {
    setComparisonLoading(true);
    setShowComparison(true);

    const sportLeague = resolveSportLeague(leagueSport);
    // Without a seasonYear OR a resolvable sport/league we can't build season-
    // specific stats/metrics URLs. Show the dialog's empty-state card rather
    // than issuing requests with an undefined year segment or a wrong sport.
    if (!seasonYear || !sportLeague) {
      setComparisonData({
        teamA: { name: matchup.away, logoUri: matchup.awayLogoUri, stats: null, metrics: null },
        teamB: { name: matchup.home, logoUri: matchup.homeLogoUri, stats: null, metrics: null }
      });
      setComparisonLoading(false);
      return;
    }

    try {
      const { sport, league } = sportLeague;

      // allSettled so a missing/failed stat block for one team doesn't kill the
      // dialog — each slot independently resolves to its data or null.
      const results = await Promise.allSettled([
        apiWrapper.TeamCard.getStatistics(sport, league, matchup.awaySlug, seasonYear, matchup.awayFranchiseSeasonId),
        apiWrapper.TeamCard.getStatistics(sport, league, matchup.homeSlug, seasonYear, matchup.homeFranchiseSeasonId),
        apiWrapper.TeamCard.getMetrics(sport, league, matchup.awaySlug, seasonYear, matchup.awayFranchiseSeasonId),
        apiWrapper.TeamCard.getMetrics(sport, league, matchup.homeSlug, seasonYear, matchup.homeFranchiseSeasonId)
      ]);

      const valueOrNull = (r) => (r.status === 'fulfilled' ? (r.value?.data ?? null) : null);
      const [awayStats, homeStats, awayMetrics, homeMetrics] = results.map(valueOrNull);

      setComparisonData({
        teamA: {
          name: matchup.away,
          logoUri: matchup.awayLogoUri,
          stats: awayStats,
          metrics: awayMetrics
        },
        teamB: {
          name: matchup.home,
          logoUri: matchup.homeLogoUri,
          stats: homeStats,
          metrics: homeMetrics
        }
      });
    } catch (e) {
      setComparisonData(null);
    } finally {
      setComparisonLoading(false);
    }
  };

  const handleCloseComparison = () => {
    setShowComparison(false);
    setComparisonData(null);
  };

  return {
    showComparison,
    comparisonLoading,
    comparisonData,
    handleOpenComparison,
    handleCloseComparison
  };
};
