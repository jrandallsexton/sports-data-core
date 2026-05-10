import { Link } from "react-router-dom";
import { contestLink } from '../../utils/sportLinks';

/**
 * Baseball per-play live block. Renders LIVE label, score with ⚾ next
 * to the batting team (derived from halfInning: Top → away batting,
 * Bottom → home batting), then three optional rows:
 *
 *   1. Inning + count + outs    (.live-state-summary)
 *   2. Runners on base          (.live-state-runners)
 *   3. Last play description    (.live-state-lastplay)
 *
 * Each optional row is suppressed when its source fields are at
 * defaults — half-inning, outs, runners, athlete IDs aren't yet
 * materialized on BaseballCompetitionPlay (see PR #308 / Issue #9
 * notes), so live MLB events arrive with mostly-default values until
 * the AtBat sourcing pipeline lands. The card degrades gracefully
 * rather than rendering "Top — · 0-0 · 0 outs" placeholders.
 *
 * Uses status-neutral class names (`.game-status-block`, `.score-display`,
 * etc.) — the broader rename of football's `.game-result` / `.final-score`
 * markup is deferred to a follow-up effort per docs/matchup-card.md.
 */
function BaseballGameStatusInProgress({
  awayShort,
  homeShort,
  awayScore,
  homeScore,
  inning,
  halfInning,
  balls,
  strikes,
  outs,
  runnerOnFirst,
  runnerOnSecond,
  runnerOnThird,
  lastPlayDescription,
  isScoringPlay,
  contestId,
  sport,
  league,
}) {
  // Top of the inning → away team is batting; Bottom → home batting.
  // Empty/unknown halfInning produces no indicator (graceful degrade).
  const half = (halfInning ?? '').toLowerCase();
  const awayIsBatting = half === 'top';
  const homeIsBatting = half === 'bottom';

  // Suppress rows whose source fields are absent. Inning is the only
  // truthiness check that needs care because PeriodNumber=0 isn't a
  // valid inning — we render the row when we have a positive inning OR
  // a non-empty half-inning string.
  const hasInningRow =
    (typeof inning === 'number' && inning > 0) ||
    (typeof halfInning === 'string' && halfInning.length > 0);
  const hasRunnersRow = runnerOnFirst || runnerOnSecond || runnerOnThird;
  const hasLastPlayRow =
    typeof lastPlayDescription === 'string' && lastPlayDescription.length > 0;

  const outsLabel = outs === 1 ? 'out' : 'outs';
  const formattedHalfInning = halfInning && inning
    ? `${halfInning} ${inning}`
    : (halfInning || (inning ? `Inning ${inning}` : ''));

  const liveContent = (
    <>
      <span className="status-label live-indicator">LIVE</span>
      <span className={`score-display ${isScoringPlay ? 'score-flash' : ''}`}>
        {awayIsBatting && <span className="possession-indicator" aria-label="batting">⚾</span>}
        {awayShort} {awayScore} - {homeScore} {homeShort}
        {homeIsBatting && <span className="possession-indicator" aria-label="batting">⚾</span>}
      </span>

      {hasInningRow && (
        <span className="live-state-summary">
          {formattedHalfInning}
          {' · '}
          {balls ?? 0}-{strikes ?? 0}
          {' · '}
          {outs ?? 0} {outsLabel}
        </span>
      )}

      {hasRunnersRow && (
        <span className="live-state-runners">
          Runners:
          {runnerOnFirst && <span className="live-state-runner-base"> 1B</span>}
          {runnerOnSecond && <span className="live-state-runner-base"> 2B</span>}
          {runnerOnThird && <span className="live-state-runner-base"> 3B</span>}
        </span>
      )}

      {hasLastPlayRow && (
        <span className="live-state-lastplay" title={lastPlayDescription}>
          {lastPlayDescription}
        </span>
      )}
    </>
  );

  return (
    <div className={`game-status-block ${isScoringPlay ? 'is-scoring-play' : ''}`}>
      <div className="game-status-row">
        {contestId ? (
          <Link
            to={contestLink(contestId, sport, league)}
            className="game-status-link"
            target="_blank"
            rel="noopener noreferrer"
          >
            {liveContent}
          </Link>
        ) : (
          liveContent
        )}
      </div>
    </div>
  );
}

export default BaseballGameStatusInProgress;
