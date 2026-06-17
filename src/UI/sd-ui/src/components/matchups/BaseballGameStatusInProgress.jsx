import { Link } from "react-router-dom";
import { contestLink } from '../../utils/sportLinks';

/**
 * Baseball per-play live block. Renders LIVE label, score with ⚾ next
 * to the batting team (derived from halfInning: Top → away batting,
 * Bottom → home batting), then up to four optional rows:
 *
 *   1. At-bat header           (.live-state-atbat)         — batter + pitcher slots
 *   2. Inning + count + outs   (.live-state-summary)
 *   3. Runners on base         (.live-state-runners)
 *   4. Last play description   (.live-state-lastplay)
 *
 * Each optional row suppresses when its source fields are absent.
 * The at-bat header per-slot suppression handles the partial-resolution
 * case (e.g., only the pitcher has been sourced yet). Each slot renders
 * just the player headshot — the headshot already uses the player's
 * team colors as its background (player-avatar engine, PR #410), so a
 * separate team mark next to it was redundant.
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
  atBatShortName,
  atBatPositionAbbreviation,
  atBatHeadshotUrl,
  pitchingShortName,
  pitchingPositionAbbreviation,
  pitchingHeadshotUrl,
  isScoringPlay,
  // When the upstream status is SUSPENDED / DELAYED / RAIN_DELAY, the
  // LIVE slot is replaced with the delay status text and styled as a
  // static muted indicator rather than the red pulsing live indicator.
  // The game isn't actively in play, so the red-pulse animation would
  // be misleading. statusDescription is the ESPN-provided human label
  // ("Suspended", "Rain Delay"); falls back to the raw status name.
  isDelayed = false,
  statusDescription,
  status,
  contestId,
  sport,
  league,
}) {
  const delayLabel = (statusDescription || status || 'PAUSED').toUpperCase();
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

  // At-bat header: offense team bats, defense team pitches. Each slot
  // is independently suppressed when its ShortName is missing so the
  // row gracefully handles partial resolution (only the pitcher has
  // been sourced yet, etc.). Whole row hides when both names are null.
  const hasAtBatRow =
    (typeof atBatShortName === 'string' && atBatShortName.length > 0) ||
    (typeof pitchingShortName === 'string' && pitchingShortName.length > 0);

  const outsLabel = outs === 1 ? 'out' : 'outs';
  const formattedHalfInning = halfInning && inning
    ? `${halfInning} ${inning}`
    : (halfInning || (inning ? `Inning ${inning}` : ''));

  const liveContent = (
    <>
      {isDelayed ? (
        <span className="status-label delay-indicator">{delayLabel}</span>
      ) : (
        <span className="status-label live-indicator">LIVE</span>
      )}
      <span className={`score-display ${isScoringPlay ? 'score-flash' : ''}`}>
        {awayIsBatting && <span className="possession-indicator" aria-label="batting">⚾</span>}
        {awayShort} {awayScore} - {homeScore} {homeShort}
        {homeIsBatting && <span className="possession-indicator" aria-label="batting">⚾</span>}
      </span>

      {hasAtBatRow && (
        <span className="live-state-atbat">
          {atBatShortName && (
            <span className="live-state-atbat-slot">
              {atBatHeadshotUrl && (
                <img
                  src={atBatHeadshotUrl}
                  alt=""
                  aria-hidden="true"
                  className="live-state-atbat-headshot"
                />
              )}
              <span className="live-state-atbat-name">{atBatShortName}</span>
              {atBatPositionAbbreviation && (
                <span className="live-state-atbat-pos">{atBatPositionAbbreviation}</span>
              )}
            </span>
          )}
          {pitchingShortName && (
            <span className="live-state-atbat-slot">
              {pitchingHeadshotUrl && (
                <img
                  src={pitchingHeadshotUrl}
                  alt=""
                  aria-hidden="true"
                  className="live-state-atbat-headshot"
                />
              )}
              <span className="live-state-atbat-name">{pitchingShortName}</span>
              {pitchingPositionAbbreviation && (
                <span className="live-state-atbat-pos">{pitchingPositionAbbreviation}</span>
              )}
            </span>
          )}
        </span>
      )}

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
