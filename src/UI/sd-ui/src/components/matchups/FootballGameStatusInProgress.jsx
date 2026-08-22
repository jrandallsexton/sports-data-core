import { Link } from "react-router-dom";
import { contestLink } from '../../utils/sportLinks';
import { formatFootballSituation } from '../../utils/liveSituation';

/**
 * Football per-play live block. Renders LIVE label, period+clock, the
 * situation line ("2nd & 7 · LV 25"), scoring flash + a typed
 * celebration label (🎉 TOUCHDOWN! / FIELD GOAL! / SAFETY! / neutral
 * SCORE! when the type is unknown - issue #45), and a last-play
 * description row (mirrors baseball).
 *
 * Possession renders on the TEAM ROW, not here: a marker beside the team
 * name is where every sports app puts it, and the scoreline that used to
 * carry it duplicated the scores already shown against each team. That
 * slot is now the situation line.
 *
 * Class names are intentionally kept (`.game-result`, `.final-score`,
 * `.score-display`, etc.) — the broader status-neutral rename is a
 * separate effort tracked in docs/matchup-card.md.
 */
function FootballGameStatusInProgress({
  awayShort,
  homeShort,
  period,
  clock,
  // Down / yards-to-go and ball spot for the situation line.
  down,
  distance,
  ballOnYardLine,
  isScoringPlay,
  // Nullable canonical scoring-type NAME slug ('touchdown', 'field-goal',
  // 'safety', 'defensive-two-point-conversion') - NOT a display name. The
  // SCORING_LABELS lookup below requires these slugs.
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
  const situation = formatFootballSituation({
    down,
    distance,
    ballOnYardLine,
    awayShort,
    homeShort,
  });

  const hasLastPlayRow =
    typeof lastPlayDescription === 'string' && lastPlayDescription.length > 0;

  // Three-tier scoring-label resolution (issue #45):
  //   1. ESPN scoringType NAME slug - closed vocabulary verified against
  //      canon: touchdown | field-goal | safety |
  //      defensive-two-point-conversion.
  //   2. Slug null (all pre-capture historical rows, so REPLAYS land here)
  //      -> sniff the play description. TD is checked FIRST so "field goal
  //      BLOCKED ... returned for a TOUCHDOWN" resolves to the actual score.
  //   3. Neutral SCORE! as the honest last resort.
  const SCORING_LABELS = {
    'touchdown': { label: 'TOUCHDOWN!', emoji: true },
    'field-goal': { label: 'FIELD GOAL!' },
    'safety': { label: 'SAFETY!' },
    'defensive-two-point-conversion': { label: 'DEF 2-PT!' },
  };
  const sniffScoringType = (text) => {
    if (!text) return null;
    if (/touchdown|\bTD\b/i.test(text)) return 'touchdown';
    if (/safety/i.test(text)) return 'safety';
    if (/field goal|\bFG\b/i.test(text)) return 'field-goal';
    return null;
  };
  const resolvedType = scoringPlayType ?? sniffScoringType(lastPlayDescription);
  const scoring = SCORING_LABELS[resolvedType] ?? { label: 'SCORE!' };

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
      {situation && (
        <span className={`situation-display ${isScoringPlay ? 'score-flash' : ''}`}>
          {situation}
        </span>
      )}
      {isScoringPlay && (
        <span className="touchdown-indicator">
          {scoring.emoji ? '🎉 ' : ''}
          {scoring.label}
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
