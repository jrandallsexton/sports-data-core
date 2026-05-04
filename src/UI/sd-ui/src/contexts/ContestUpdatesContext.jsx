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
   * Handle ContestStatusChanged (lifecycle) event from SignalR.
   * Sport-neutral — only updates the lifecycle status field.
   * Per-play scoreboard ticks come in via handleFootballStateUpdate /
   * handleBaseballStateUpdate.
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
   * Handle FootballContestStateChanged (per-play scoreboard tick) event.
   * Updates period, clock, scores, possession, scoring-play flash. Does
   * not touch the lifecycle status field — that's owned by
   * ContestStatusChanged.
   */
  const handleFootballStateUpdate = useCallback((data) => {
    if (!data?.contestId) {
      console.warn('FootballContestStateChanged event missing contestId', data);
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
      }, 2000); // Clear after 2 seconds
    }
  }, []);

  /**
   * Handle BaseballContestStateChanged (per-pitch / per-at-bat tick).
   * Mirrors handleFootballStateUpdate but with the baseball shape
   * (inning, count, outs, base state, current at-bat).
   */
  const handleBaseballStateUpdate = useCallback((data) => {
    if (!data?.contestId) {
      console.warn('BaseballContestStateChanged event missing contestId', data);
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
        lastUpdated: Date.now()
      }
    }));
  }, []);

  /**
   * Handle ContestPlayCompleted (sport-neutral per-play log) event.
   * Stores the latest play description on the contest record so the
   * UI can render a play-by-play feed without needing the full play
   * object. PlayId lets the consumer dedupe if it cares.
   */
  const handlePlayCompleted = useCallback((data) => {
    if (!data?.contestId) {
      console.warn('ContestPlayCompleted event missing contestId', data);
      return;
    }

    setContests(prev => ({
      ...prev,
      [data.contestId]: {
        ...prev[data.contestId],
        contestId: data.contestId,
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
    handleFootballStateUpdate,
    handleBaseballStateUpdate,
    handlePlayCompleted,
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
