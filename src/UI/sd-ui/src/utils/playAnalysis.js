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

/**
 * The facts a card shows: the live (SignalR) facts for the last play, else the
 * facts that came with the matchup payload (cold-load, task 13) while the
 * payload's last play is still the card's last play.
 *
 * @param {object|null|undefined} liveUpdate - a ContestUpdatesContext entry
 * @param {object} matchup - the fetched matchup (lastPlayId, playAnalysis)
 * @returns {Array<{id: string, factType: string, text: string}>|null}
 */
export function playAnalysisForCard(liveUpdate, matchup) {
  const live = playAnalysisForLastPlay(liveUpdate);
  if (live) {
    return live;
  }
  const lastPlayId = liveUpdate?.lastPlayId ?? matchup?.lastPlayId;
  if (!lastPlayId || matchup?.lastPlayId !== lastPlayId) {
    return null;
  }
  return matchup.playAnalysis?.length ? matchup.playAnalysis : null;
}

/** How many facts a card shows under the last-play line; the rest are still delivered. */
export const PLAY_ANALYSIS_MAX_SHOWN = 2;
