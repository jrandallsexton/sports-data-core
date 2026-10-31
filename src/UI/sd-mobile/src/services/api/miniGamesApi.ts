import { apiClient } from './client';
import type {
  MiniGameResult,
  MiniGameStats,
  SubmitMiniGameResultsResponse,
} from '@/src/utils/minigames/types';

/**
 * Wrapper for /user/me/minigames/* -- the signed-in user's mini-game results.
 * See docs/features/mini-game-results.md.
 */
export const miniGamesApi = {
  // POST /user/me/minigames/results — up to 100 per call; idempotent per result id.
  submitResults: (results: MiniGameResult[]) =>
    apiClient.post<SubmitMiniGameResultsResponse>('/user/me/minigames/results', { results }),

  // GET /user/me/minigames/{gameKey}/stats → raw totals, streaks, last 10.
  getStats: (gameKey: string) =>
    apiClient.get<MiniGameStats>(`/user/me/minigames/${encodeURIComponent(gameKey)}/stats`),
};
