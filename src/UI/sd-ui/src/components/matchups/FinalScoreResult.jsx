import { FaCheck } from "react-icons/fa";

// Quick-scan result row appended below the final score for ATS / O/U
// leagues. SU is NOT rendered here — its checkmark is inline next to
// the winning team's short directly in GameStatus.jsx's scoreContent
// (the score line already shows the winning team, so a duplicated
// "NYY won" suffix would be noise).
//
// Pure presentation — does NOT reflect the user's own pick (PickButton
// handles that). The goal is letting a user glance at a finalized card
// and confirm the result relevant to the league's pick mode without
// parsing the score themselves.
function FinalScoreResult({
  pickType,
  awayFranchiseSeasonId,
  homeFranchiseSeasonId,
  awayShort,
  homeShort,
  spreadWinnerFranchiseSeasonId,
  overUnderResult,
  overUnderCurrent,
}) {
  const shortFor = (franchiseSeasonId) => {
    if (!franchiseSeasonId) return null;
    if (franchiseSeasonId === awayFranchiseSeasonId) return awayShort;
    if (franchiseSeasonId === homeFranchiseSeasonId) return homeShort;
    return null;
  };

  if (pickType === "AgainstTheSpread") {
    // Null spread-winner = push at the spread (Producer enrichment sets
    // SpreadWinnerFranchiseId to null when the game lands exactly on the
    // line, see ContestEnrichmentProcessor).
    if (!spreadWinnerFranchiseSeasonId) {
      return (
        <div className="final-score-result final-score-result-push">Push</div>
      );
    }
    const cover = shortFor(spreadWinnerFranchiseSeasonId);
    if (!cover) return null;
    return (
      <div className="final-score-result">
        <FaCheck className="final-score-result-icon" />
        <span>{cover} covered</span>
      </div>
    );
  }

  if (pickType === "OverUnder") {
    if (!overUnderResult || overUnderResult === "None") {
      return (
        <div className="final-score-result final-score-result-push">Push</div>
      );
    }
    const ouValue =
      overUnderCurrent !== null && overUnderCurrent !== undefined
        ? ` ${overUnderCurrent}`
        : "";
    return (
      <div className="final-score-result">
        <FaCheck className="final-score-result-icon" />
        <span>
          {overUnderResult}
          {ouValue}
        </span>
      </div>
    );
  }

  // StraightUp (and unknown pickType) — no row, handled inline.
  return null;
}

export default FinalScoreResult;
