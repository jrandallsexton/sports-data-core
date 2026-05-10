import { Link } from "react-router-dom";
import { contestLink } from '../../utils/sportLinks';

/**
 * Football per-play live block. Lifted verbatim from GameStatus.jsx so
 * the per-sport split (FB vs MLB) can land without disturbing the
 * existing football layout. Behavior is unchanged: LIVE label,
 * period+clock, score with 🏈 next to the team in possession, scoring
 * flash + 🎉 TOUCHDOWN! indicator.
 *
 * Class names are intentionally kept (`.game-result`, `.final-score`,
 * `.score-display`, etc.) — the broader status-neutral rename is a
 * separate effort tracked in docs/matchup-card.md and is out of scope
 * for the MLB-first enrichment PR.
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
  contestId,
  sport,
  league,
}) {
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

  const liveContent = (
    <>
      <span className="result-label live-indicator">LIVE</span>
      {period && clock && (
        <span className="game-clock">{period} - {clock}</span>
      )}
      <span className={`score-display ${isScoringPlay ? 'score-flash' : ''}`}>
        {awayHasPossession && <span className="possession-indicator">🏈</span>}
        {awayShort} {awayScore} - {homeScore} {homeShort}
        {homeHasPossession && <span className="possession-indicator">🏈</span>}
      </span>
      {isScoringPlay && (
        // TODO: Determine score type (TD, FG, etc.) for better indicator
        <span className="touchdown-indicator">🎉 TOUCHDOWN!</span>
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
