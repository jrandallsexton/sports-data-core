import "./MatchupCard.css";
import { FaChartLine, FaLock, FaCheckCircle, FaTimes } from "react-icons/fa";
import { Link } from "react-router-dom";
import { formatToEasternTime } from "../../utils/timeUtils";
import { useState, useEffect } from "react";

function MatchupCard({
  matchup,
  userPickFranchiseSeasonId,
  userPickResult, // New: DTO containing isCorrect, franchiseId, etc.
  onPick,
  onViewInsight,
  isInsightUnlocked,
  isFadingOut = false
}) {
  const homeSpread = matchup.homeSpread ?? 0;
  const overUnder = matchup.overUnder ?? "TBD";
  const gameTime = formatToEasternTime(matchup.startDateUtc);
  const venue = matchup.venue ?? "TBD";
  const location = `${matchup.venueCity ?? ""}, ${matchup.venueState ?? ""}`;
  const seasonYear = matchup.seasonYear ?? 2025;

  // Determine pick result when game is complete
  const getUserPickResult = () => {
    if (!matchup.isComplete) return null;
    
    // Use server-calculated result if available
    if (userPickResult) {
      return userPickResult.isCorrect ? 'correct' : 'incorrect';
    }
    
    // Fallback to client-side calculation (should be rare/deprecated)
    if (userPickFranchiseSeasonId) {
      const userPickedCorrect = userPickFranchiseSeasonId === matchup.winnerFranchiseSeasonId;
      return userPickedCorrect ? 'correct' : 'incorrect';
    }
    
    return null;
  };

  const pickResult = getUserPickResult();

  // Determine selected team using pick result data or fallback to prop
  const selectedFranchiseId = userPickResult?.franchiseId || userPickFranchiseSeasonId;

  const isAwaySelected =
    selectedFranchiseId === matchup.awayFranchiseSeasonId;
  const isHomeSelected =
    selectedFranchiseId === matchup.homeFranchiseSeasonId;

  const [now, setNow] = useState(new Date());

  useEffect(() => {
    const interval = setInterval(() => {
      setNow(new Date());
    }, 15000); // check every 15 seconds

    return () => clearInterval(interval); // cleanup on unmount
  }, []);

  const startTime = new Date(matchup.startDateUtc);
  const lockTime = new Date(startTime.getTime() - 5 * 60 * 1000); // subtract 5 minutes
  const isLocked = now > lockTime;

  const getCardBorderClass = () => {
    if (!matchup.isComplete) return ""; // No border for incomplete games
    
    // Check if user made a pick (either in new or old format)
    if (!userPickResult && !userPickFranchiseSeasonId) return "pick-no-submission"; // Red border for no pick
    
    return pickResult ? `pick-${pickResult}` : ""; // Green/red based on result
  };

  return (
    <div className={`matchup-card ${isFadingOut ? "fade-out" : ""} ${getCardBorderClass()}`}>
      {/* Away Team Row */}
      <div className="team-row">
        <div className="team-info">
          {matchup.awayLogoUri && (
            <img
              src={matchup.awayLogoUri}
              alt={`${matchup.away} logo`}
              className="matchup-logo"
            />
          )}
          <div className="team-details">
            <div className="team-name-row">
              {matchup.awayRank && (
                <span className="team-ranking">#{matchup.awayRank}</span>
              )}
              <Link
                to={`/app/sport/football/ncaa/team/${matchup.awaySlug}/${seasonYear}`}
                className="team-link"
              >
                {matchup.away}
              </Link>
            </div>
            <div className="team-record">
              <span>
                Overall: {matchup.awayWins}-{matchup.awayLosses}
              </span>
              <span>
                Conference: {matchup.awayConferenceWins}-
                {matchup.awayConferenceLosses}
              </span>
            </div>
          </div>
        </div>
      </div>

      <div className="at-divider">at</div>

      {/* Home Team Row */}
      <div className="team-row">
        <div className="team-info">
          {matchup.homeLogoUri && (
            <img
              src={matchup.homeLogoUri}
              alt={`${matchup.home} logo`}
              className="matchup-logo"
            />
          )}
          <div className="team-details">
            <div className="team-name-row">
              {matchup.homeRank && (
                <span className="team-ranking">#{matchup.homeRank}</span>
              )}
              <Link
                to={`/app/sport/football/ncaa/team/${matchup.homeSlug}/${seasonYear}`}
                className="team-link"
              >
                {matchup.home}
              </Link>
            </div>
            <div className="team-record">
              <span>
                Overall: {matchup.homeWins}-{matchup.homeLosses}
              </span>
              <span>
                Conference: {matchup.homeConferenceWins}-
                {matchup.homeConferenceLosses}
              </span>
            </div>
          </div>
        </div>
        <div className="team-spread">
          {homeSpread > 0 ? `+${homeSpread}` : homeSpread}
        </div>
      </div>

      {/* Game Result - show final score if game is complete */}
      {matchup.isComplete ? (
        <div className="game-result">
          <div className="final-score">
            <span className="result-label">FINAL:</span>
            <span className="score-display">
              {matchup.awayShort} {matchup.awayScore} - {matchup.homeScore} {matchup.homeShort}
            </span>
          </div>
        </div>
      ) : (
        <div className="game-time-location">
          {gameTime} | {venue} | {location}
        </div>
      )}

      <div className="spread-ou">O/U: {overUnder}</div>

      <div className="pick-buttons">
        <button
          className={`pick-button ${isAwaySelected ? "selected" : ""} ${
            pickResult && isAwaySelected ? `result-${pickResult}` : ""
          }`}
          onClick={() => onPick(matchup, matchup.awayFranchiseSeasonId)}
          disabled={isLocked}
        >
          {/* Show result icons for completed games */}
          {pickResult && isAwaySelected && pickResult === 'correct' && <FaCheckCircle className="pick-result-icon" />}
          {pickResult && isAwaySelected && pickResult === 'incorrect' && <FaTimes className="pick-result-icon" />}
          {/* Show normal pick/lock icons for ongoing games */}
          {!pickResult && isAwaySelected && !isLocked && <FaCheckCircle className="pick-check-icon" />}
          {!pickResult && isAwaySelected && isLocked && <FaLock className="pick-lock-icon" />}
          {!pickResult && !isAwaySelected && isLocked && <FaLock className="pick-lock-icon" />}
          {matchup.awayShort}
        </button>

        <button
          className="insight-button"
          onClick={() => onViewInsight(matchup)}
          disabled={
            !matchup.isPreviewAvailable || !isInsightUnlocked
          }
          title={
            !matchup.isPreviewAvailable
              ? "Preview not available"
              : isInsightUnlocked
              ? "View Insight"
              : "Unlock Insights with Subscription"
          }
        >
          {isInsightUnlocked ? <FaChartLine /> : <FaLock />}
        </button>

        <button
          className={`pick-button ${isHomeSelected ? "selected" : ""} ${
            pickResult && isHomeSelected ? `result-${pickResult}` : ""
          }`}
          onClick={() => onPick(matchup, matchup.homeFranchiseSeasonId)}
          disabled={isLocked}
        >
          {/* Show result icons for completed games */}
          {pickResult && isHomeSelected && pickResult === 'correct' && <FaCheckCircle className="pick-result-icon" />}
          {pickResult && isHomeSelected && pickResult === 'incorrect' && <FaTimes className="pick-result-icon" />}
          {/* Show normal pick/lock icons for ongoing games */}
          {!pickResult && isHomeSelected && !isLocked && <FaCheckCircle className="pick-check-icon" />}
          {!pickResult && isHomeSelected && isLocked && <FaLock className="pick-lock-icon" />}
          {!pickResult && !isHomeSelected && isLocked && <FaLock className="pick-lock-icon" />}
          {matchup.homeShort}
        </button>
      </div>
    </div>
  );
}

export default MatchupCard;
