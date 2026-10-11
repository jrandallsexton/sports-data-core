/**
 * Instant analysis (docs/features/instant-analysis/): the facts a card should
 * show for a contest's live update, or null.
 *
 * Facts belong to one play (keyed by playId) and show only while that play is
 * the contest's last play: an analysis that arrived before its play waits
 * until the play lands, and one for an older play is never shown.
 *
 * @param {object|null|undefined} liveUpdate - a ContestUpdatesContext entry
 * @returns {Array<{id: string, factType: string, text: string}>|null}
 */
export function playAnalysisForLastPlay(liveUpdate) {
  const analysis = liveUpdate?.lastPlayAnalysis;
  if (!analysis || !liveUpdate.lastPlayId || analysis.playId !== liveUpdate.lastPlayId) {
    return null;
  }
  return analysis.items?.length ? analysis.items : null;
}

/** How many facts a card shows under the last-play line; the rest are still delivered. */
export const PLAY_ANALYSIS_MAX_SHOWN = 2;
