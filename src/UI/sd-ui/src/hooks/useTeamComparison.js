import { useState } from 'react';
import apiWrapper from '../api/apiWrapper';

/**
 * Custom hook to manage team comparison dialog state and data fetching
 * @param {object} matchup - Matchup data object
 * @param {number} seasonYear - Season year for the comparison data
 * @returns {object} { showComparison, comparisonLoading, comparisonData, handleOpenComparison, handleCloseComparison }
 */
export const useTeamComparison = (matchup, seasonYear) => {
  const [showComparison, setShowComparison] = useState(false);
  const [comparisonLoading, setComparisonLoading] = useState(false);
  const [comparisonData, setComparisonData] = useState(null);

  const handleOpenComparison = async () => {
    setComparisonLoading(true);
    setShowComparison(true);

    try {
      const [awayRes, homeRes, awayMetrics, homeMetrics] = await Promise.all([
        apiWrapper.TeamCard.getStatistics(matchup.awaySlug, seasonYear, matchup.awayFranchiseSeasonId),
        apiWrapper.TeamCard.getStatistics(matchup.homeSlug, seasonYear, matchup.homeFranchiseSeasonId),
        apiWrapper.TeamCard.getMetrics(matchup.awaySlug, seasonYear, matchup.awayFranchiseSeasonId),
        apiWrapper.TeamCard.getMetrics(matchup.homeSlug, seasonYear, matchup.homeFranchiseSeasonId)
      ]);

      setComparisonData({
        teamA: {
          name: matchup.away,
          logoUri: matchup.awayLogoUri,
          stats: awayRes.data,
          metrics: awayMetrics.data
        },
        teamB: {
          name: matchup.home,
          logoUri: matchup.homeLogoUri,
          stats: homeRes.data,
          metrics: homeMetrics.data
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
