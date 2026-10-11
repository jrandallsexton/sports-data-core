import type { ContestLiveRecord } from '@/src/stores/contestUpdatesStore';
import type { Matchup } from '@/src/types/models';
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

/**
 * The facts a card shows: the live (SignalR) facts for the last play, else the
 * facts that came with the matchup payload (cold-load, task 13) while the
 * payload's last play is still the card's last play. Mirrors web's
 * playAnalysisForCard.
 */
export function playAnalysisForCard(
  live: ContestLiveRecord | undefined,
  matchup: Pick<Matchup, 'lastPlayId' | 'playAnalysis'>,
): PlayAnalysisItem[] | null {
  const fromLive = playAnalysisForLastPlay(live);
  if (fromLive) return fromLive;

  const lastPlayId = live?.lastPlayId ?? matchup.lastPlayId;
  if (!lastPlayId || matchup.lastPlayId !== lastPlayId) return null;
  return matchup.playAnalysis?.length ? matchup.playAnalysis : null;
}

/** How many facts a card shows under the last-play line; the rest are still delivered. */
export const PLAY_ANALYSIS_MAX_SHOWN = 2;
