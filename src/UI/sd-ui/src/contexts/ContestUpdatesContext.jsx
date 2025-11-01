import { createContext, useContext, useState, useCallback } from 'react';

/**
 * Context for managing real-time contest updates from SignalR
 * Stores live game data including scores, status, possession, and clock
 */
const ContestUpdatesContext = createContext(null);

export const ContestUpdatesProvider = ({ children }) => {
  // Map of contestId -> live update data
  const [contests, setContests] = useState({});

  /**
   * Handle ContestStatusUpdated event from SignalR
   * Updates game status, scores, period, clock, and possession
   */
  const handleStatusUpdate = useCallback((data) => {
    if (!data?.contestId) {
      console.warn('ContestStatusUpdated event missing contestId', data);
      return;
    }

    setContests(prev => ({
      ...prev,
      [data.contestId]: {
        ...prev[data.contestId],
        contestId: data.contestId,
        status: data.status,
        period: data.period,
        clock: data.clock,
        awayScore: data.awayScore,
        homeScore: data.homeScore,
        possessionFranchiseSeasonId: data.possessionFranchiseSeasonId,
        lastUpdated: Date.now()
      }
    }));
  }, []);

  /**
   * Get live update data for a specific contest
   * @param {string} contestId 
   * @returns {object|null} Live update data or null if no updates available
   */
  const getContestUpdate = useCallback((contestId) => {
    return contests[contestId] || null;
  }, [contests]);

  /**
   * Check if a contest has any live updates
   * @param {string} contestId 
   * @returns {boolean}
   */
  const hasLiveUpdate = useCallback((contestId) => {
    return !!contests[contestId];
  }, [contests]);

  /**
   * Clear updates for a specific contest (e.g., when game ends)
   * @param {string} contestId 
   */
  const clearContestUpdate = useCallback((contestId) => {
    setContests(prev => {
      const { [contestId]: removed, ...rest } = prev;
      return rest;
    });
  }, []);

  /**
   * Clear all contest updates (e.g., on logout or league change)
   */
  const clearAllUpdates = useCallback(() => {
    setContests({});
  }, []);

  const value = {
    contests,
    handleStatusUpdate,
    getContestUpdate,
    hasLiveUpdate,
    clearContestUpdate,
    clearAllUpdates
  };

  return (
    <ContestUpdatesContext.Provider value={value}>
      {children}
    </ContestUpdatesContext.Provider>
  );
};

/**
 * Hook to access contest updates context
 * @returns {object} Contest updates context value
 */
export const useContestUpdates = () => {
  const context = useContext(ContestUpdatesContext);
  if (!context) {
    throw new Error('useContestUpdates must be used within ContestUpdatesProvider');
  }
  return context;
};

export default ContestUpdatesContext;
