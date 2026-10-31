// Mini-game results and stats (docs/features/mini-game-results.md). Generic
// across mini-games: everything is keyed by gameKey. Shapes mirror the API's
// MiniGamesController.

export type MiniGameOutcome = 'Win' | 'Loss' | 'Tie';

/** One finished game. `id` is minted on the device so re-uploads are no-ops. */
export interface MiniGameResult {
  id: string;
  gameKey: string;
  outcome: MiniGameOutcome;
  score: number;
  opponentScore: number;
  durationSeconds: number;
  gameVersion: string;
  seed?: string;
  /** ISO-8601 UTC. */
  playedUtc: string;
}

/** A result waiting to upload, tagged with the account that played it. */
export interface QueuedMiniGameResult extends MiniGameResult {
  ownerUid: string;
}

export interface MiniGameRecentResult {
  id: string;
  outcome: MiniGameOutcome;
  score: number;
  opponentScore: number;
  playedUtc: string;
}

/** Raw totals (not averages) so queued results merge in exactly. */
export interface MiniGameStats {
  wins: number;
  losses: number;
  ties: number;
  winMarginTotal: number;
  lossMarginTotal: number;
  currentStreak: { outcome: MiniGameOutcome; length: number } | null;
  bestWinStreak: number;
  /** Newest first. */
  recent: MiniGameRecentResult[];
}

export interface SubmitMiniGameResultsResponse {
  accepted: string[];
  duplicate: string[];
  rejected: string[];
}

export function outcomeOf(score: number, opponentScore: number): MiniGameOutcome {
  if (score > opponentScore) return 'Win';
  if (score < opponentScore) return 'Loss';
  return 'Tie';
}
