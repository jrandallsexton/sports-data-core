import { Link } from "react-router-dom";
import { contestLink } from '../../utils/sportLinks';

/**
 * Football per-play live block. Renders LIVE label, period+clock, score
 * with 🏈 next to the team in possession, scoring flash + a typed
 * celebration label (🎉 TOUCHDOWN! / FIELD GOAL! / SAFETY! / neutral
 * SCORE! when the type is unknown - issue #45), and a last-play
 * description row (mirrors baseball).
 *
 * Class names are intentionally kept (`.game-result`, `.final-score`,
 * `.score-display`, etc.) — the broader status-neutral rename is a
 * separate effort tracked in docs/matchup-card.md.
 */
function FootballGameStatusInProgress({
  awayShort,
  homeShort,
  awayScore,
  homeScore,
  period,
  clock,
  awayFranchiseSeasonId,
  homeFranchiseSeasonId,
  possessionFranchiseSeasonId,
  isScoringPlay,
  // ESPN scoringType displayName ("Touchdown", "Field Goal", ...) or null.
  scoringPlayType,
  lastPlayDescription,
  // See BaseballGameStatusInProgress isDelayed comment — same
  // semantics, mirrors the LIVE → delay-status text swap.
  isDelayed = false,
  statusDescription,
  status,
  contestId,
  sport,
  league,
}) {
  const delayLabel = (statusDescription || status || 'PAUSED').toUpperCase();
  // Guard against null/undefined possession — without the != null check,
  // a missing possessionFranchiseSeasonId could match a missing
  // awayFranchiseSeasonId / homeFranchiseSeasonId via `===` and falsely
  // mark a team as having possession.
  const awayHasPossession =
    possessionFranchiseSeasonId != null
    && possessionFranchiseSeasonId === awayFranchiseSeasonId;
  const homeHasPossession =
    possessionFranchiseSeasonId != null
    && possessionFranchiseSeasonId === homeFranchiseSeasonId;

  const hasLastPlayRow =
    typeof lastPlayDescription === 'string' && lastPlayDescription.length > 0;

  // ESPN's displayName is human-ready ("Field Goal") - uppercase it rather
  // than maintaining a lookup table. The party emoji stays reserved for
  // touchdowns; unknown-but-scoring falls back to a neutral SCORE!.
  const isTouchdown = /touchdown/i.test(scoringPlayType || '');
  const scoringLabel = scoringPlayType
    ? `${scoringPlayType.toUpperCase()}!`
    : 'SCORE!';

  const liveContent = (
    <>
      {isDelayed ? (
        <span className="result-label delay-indicator">{delayLabel}</span>
      ) : (
        <span className="result-label live-indicator">LIVE</span>
      )}
      {period && clock && (
        <span className="game-clock">{period} - {clock}</span>
      )}
      <span className={`score-display ${isScoringPlay ? 'score-flash' : ''}`}>
        {awayHasPossession && <span className="possession-indicator">🏈</span>}
        {awayShort} {awayScore} - {homeScore} {homeShort}
        {homeHasPossession && <span className="possession-indicator">🏈</span>}
      </span>
      {isScoringPlay && (
        <span className="touchdown-indicator">
          {isTouchdown ? '🎉 ' : ''}
          {scoringLabel}
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
    <div className={`game-result ${isScoringPlay ? 'scoring-play' : ''}`}>
      <div className="final-score">
        {contestId ? (
          <Link
            to={contestLink(contestId, sport, league)}
            className="final-score-link"
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

export default FootballGameStatusInProgress;
