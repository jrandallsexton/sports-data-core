import { createContext, useContext, useState, useCallback } from 'react';

/**
 * Context for managing real-time contest updates from SignalR.
 * Stores live game data including lifecycle status, play description,
 * scores, possession (FB), inning/count/runners (MLB), and clock.
 */
const ContestUpdatesContext = createContext(null);

export const ContestUpdatesProvider = ({ children }) => {
  // Map of contestId -> live update data
  const [contests, setContests] = useState({});

  /**
   * Handle ContestStatusChanged (lifecycle) event from SignalR.
   * Sport-neutral — only updates the lifecycle status field.
   * Per-play updates land via handleFootballPlayCompleted /
   * handleBaseballPlayCompleted, which carry both the play description
   * and the sport-specific scoreboard tick in one event.
   */
  const handleStatusUpdate = useCallback((data) => {
    if (!data?.contestId) {
      console.warn('ContestStatusChanged event missing contestId', data);
      return;
    }

    setContests(prev => ({
      ...prev,
      [data.contestId]: {
        ...prev[data.contestId],
        contestId: data.contestId,
        status: data.status,
        lastUpdated: Date.now()
      }
    }));
  }, []);

  /**
   * Handle FootballPlayCompleted — merged per-play event carrying both
   * the play description and the football scoreboard tick (period,
   * clock, score, possession, scoring flash, ball position). Replaces
   * the prior split between FootballContestStateChanged and the
   * sport-neutral ContestPlayCompleted.
   */
  const handleFootballPlayCompleted = useCallback((data) => {
    if (!data?.contestId) {
      console.warn('FootballPlayCompleted event missing contestId', data);
      return;
    }

    setContests(prev => ({
      ...prev,
      [data.contestId]: {
        ...prev[data.contestId],
        contestId: data.contestId,
        period: data.period,
        clock: data.clock,
        awayScore: data.awayScore,
        homeScore: data.homeScore,
        possessionFranchiseSeasonId: data.possessionFranchiseSeasonId,
        isScoringPlay: data.isScoringPlay || false,
        ballOnYardLine: data.ballOnYardLine,
        lastPlayId: data.playId,
        lastPlayDescription: data.playDescription,
        lastPlayAt: Date.now(),
        lastUpdated: Date.now()
      }
    }));

    // Auto-clear scoring play flag after animation duration
    if (data.isScoringPlay) {
      setTimeout(() => {
        setContests(prev => ({
          ...prev,
          [data.contestId]: {
            ...prev[data.contestId],
            isScoringPlay: false
          }
        }));
      }, 2000);
    }
  }, []);

  /**
   * Handle BaseballPlayCompleted — merged per-play event carrying both
   * the play description and the baseball scoreboard tick (inning,
   * half-inning, count, outs, base state, current at-bat / pitcher).
   * Replaces the prior split between BaseballContestStateChanged and
   * the sport-neutral ContestPlayCompleted.
   */
  const handleBaseballPlayCompleted = useCallback((data) => {
    if (!data?.contestId) {
      console.warn('BaseballPlayCompleted event missing contestId', data);
      return;
    }

    setContests(prev => ({
      ...prev,
      [data.contestId]: {
        ...prev[data.contestId],
        contestId: data.contestId,
        inning: data.inning,
        halfInning: data.halfInning,
        awayScore: data.awayScore,
        homeScore: data.homeScore,
        balls: data.balls,
        strikes: data.strikes,
        outs: data.outs,
        runnerOnFirst: data.runnerOnFirst,
        runnerOnSecond: data.runnerOnSecond,
        runnerOnThird: data.runnerOnThird,
        atBatAthleteId: data.atBatAthleteId,
        pitchingAthleteId: data.pitchingAthleteId,
        lastPlayId: data.playId,
        lastPlayDescription: data.playDescription,
        lastPlayAt: Date.now(),
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
    handleFootballPlayCompleted,
    handleBaseballPlayCompleted,
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
