import { createContext, useContext, useState, useCallback } from 'react';

// DIAG (refresh-loses-updates investigation): module-level counter so
// each ContestUpdatesProvider mount gets a unique instance ID. If the
// console shows logs tagged with different IDs (e.g. "#1" and "#2"),
// the React tree has two Provider instances and SignalR callbacks may
// be writing to one while consumers read from the other.
let _ctxInstanceCounter = 0;

/**
 * Normalize an ESPN-style status string to the PascalCase form the UI
 * components branch on. The backend's ContestStatusChanged event carries
 * the raw ESPN value verbatim ("STATUS_FINAL", "STATUS_IN_PROGRESS",
 * "STATUS_POSTPONED"), but GameStatus / BoxScoreTable / ContestOverview
 * compare against "Final" / "InProgress" / "Postponed". Without this
 * normalization the lifecycle event lands in state but every branch
 * misses, so the card stays on its previous (e.g. live) layout.
 *
 * Pass-through for values that don't look like the ESPN form so any
 * already-normalized status (e.g. set by the *PlayCompleted handlers)
 * is preserved unchanged.
 */
const normalizeStatus = (raw) => {
  if (typeof raw !== 'string' || raw.length === 0) return raw;
  if (!raw.includes('_')) return raw;
  const stripped = raw.startsWith('STATUS_') ? raw.slice('STATUS_'.length) : raw;
  return stripped
    .toLowerCase()
    .split('_')
    .map(part => part.charAt(0).toUpperCase() + part.slice(1))
    .join('');
};

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

    const normalized = normalizeStatus(data.status);
    console.log(`[ContestCtx#${instanceId}] setContests (status)`, { contestId: data.contestId, status: normalized, raw: data.status });
    setContests(prev => ({
      ...prev,
      [data.contestId]: {
        ...prev[data.contestId],
        contestId: data.contestId,
        status: normalized,
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
        // Receiving a play is itself proof the contest is live, so
        // promote status to InProgress here even if the prior
        // ContestStatusChanged lifecycle event was missed (e.g. a
        // refresh happened after the replay's one-shot lifecycle
        // fan-out — SignalR has no buffer, so post-connect clients
        // miss any event published before their handshake completed).
        // Without this, GameStatus stays on the Final/Scheduled
        // branch and never renders the merged live data.
        status: 'InProgress',
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
        // Receiving a play is itself proof the contest is live — see
        // matching comment in handleFootballPlayCompleted. Without
        // this, refresh-after-replay-start leaves the card stuck on
        // the Final layout despite plays flowing into context.
        status: 'InProgress',
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
