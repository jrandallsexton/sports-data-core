import { createContext, useContext, useState, useCallback } from 'react';

// DIAG (refresh-loses-updates investigation): module-level counter so
// each ContestUpdatesProvider mount gets a unique instance ID. If the
// console shows logs tagged with different IDs (e.g. "#1" and "#2"),
// the React tree has two Provider instances and SignalR callbacks may
// be writing to one while consumers read from the other.
let _ctxInstanceCounter = 0;

/**
 * Context for managing real-time contest updates from SignalR.
 * Stores live game data including lifecycle status, play description,
 * scores, possession (FB), inning/count/runners (MLB), and clock.
 */
const ContestUpdatesContext = createContext(null);

export const ContestUpdatesProvider = ({ children }) => {
  // DIAG: stable instance ID for this Provider's lifetime (lazy init via useState).
  const [instanceId] = useState(() => ++_ctxInstanceCounter);
  // Map of contestId -> live update data
  const [contests, setContests] = useState({});

  console.log(`[ContestCtx#${instanceId}] render, contests size:`, Object.keys(contests).length, 'keys:', Object.keys(contests));

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

    console.log(`[ContestCtx#${instanceId}] setContests (status)`, { contestId: data.contestId, status: data.status });
    setContests(prev => ({
      ...prev,
      [data.contestId]: {
        ...prev[data.contestId],
        contestId: data.contestId,
        status: data.status,
        lastUpdated: Date.now()
      }
    }));
  }, [instanceId]);

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

    console.log(`[ContestCtx#${instanceId}] setContests (football play)`, { contestId: data.contestId, period: data.period, clock: data.clock });
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
  }, [instanceId]);

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

    console.log(`[ContestCtx#${instanceId}] setContests (baseball play)`, { contestId: data.contestId, inning: data.inning, half: data.halfInning });
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
        atBatAthleteSeasonId: data.atBatAthleteSeasonId,
        atBatShortName: data.atBatShortName,
        atBatPositionAbbreviation: data.atBatPositionAbbreviation,
        atBatHeadshotUrl: data.atBatHeadshotUrl,
        pitchingAthleteSeasonId: data.pitchingAthleteSeasonId,
        pitchingShortName: data.pitchingShortName,
        pitchingPositionAbbreviation: data.pitchingPositionAbbreviation,
        pitchingHeadshotUrl: data.pitchingHeadshotUrl,
        lastPlayId: data.playId,
        lastPlayDescription: data.playDescription,
        lastPlayAt: Date.now(),
        lastUpdated: Date.now()
      }
    }));
  }, [instanceId]);

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
    _instanceId: instanceId, // DIAG: lets consumers log which Provider they're reading from
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
