import type { ContestLiveRecord } from '@/src/stores/contestUpdatesStore';
import type { PlayAnalysisItem } from '@/src/types/signalR';

/**
 * Instant analysis (docs/features/instant-analysis/): the facts a card should
 * show for a contest's live record, or null.
 *
 * Facts belong to one play (keyed by playId) and show only while that play is
 * the contest's last play: an analysis that arrived before its play waits
 * until the play lands, and one for an older play is never shown. Mirrors
 * web's utils/playAnalysis.js.
 */
export function playAnalysisForLastPlay(
  live: ContestLiveRecord | undefined,
): PlayAnalysisItem[] | null {
  const analysis = live?.lastPlayAnalysis;
  if (!analysis || !live?.lastPlayId || analysis.playId !== live.lastPlayId) {
    return null;
  }
  return analysis.items.length > 0 ? analysis.items : null;
}

/** How many facts a card shows under the last-play line; the rest are still delivered. */
export const PLAY_ANALYSIS_MAX_SHOWN = 2;
