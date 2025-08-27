import "./MatchupCard.css";
import { FaChartLine, FaLock, FaCheckCircle } from "react-icons/fa";
import { Link } from "react-router-dom";
import { formatToEasternTime } from "../../utils/timeUtils";
import { useState, useEffect } from "react";

function MatchupCard({
  matchup,
  userPickFranchiseSeasonId,
  onPick,
  onViewInsight,
  isInsightUnlocked,
  isFadingOut = false
}) {
  const awaySpread = matchup.awaySpread ?? 0;
  const homeSpread = matchup.homeSpread ?? 0;
  const overUnder = matchup.overUnder ?? "TBD";
  const gameTime = formatToEasternTime(matchup.startDateUtc);
  const venue = matchup.venue ?? "TBD";
  const location = `${matchup.venueCity ?? ""}, ${matchup.venueState ?? ""}`;
  const seasonYear = matchup.seasonYear ?? 2025;

  const isAwaySelected =
    userPickFranchiseSeasonId === matchup.awayFranchiseSeasonId;
  const isHomeSelected =
    userPickFranchiseSeasonId === matchup.homeFranchiseSeasonId;

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

  return (
    <div className={`matchup-card ${isFadingOut ? "fade-out" : ""}`}>
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
        <div className="team-spread">
          {awaySpread > 0 ? `+${awaySpread}` : awaySpread}
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

      <div className="game-time-location">
        {gameTime} | {venue} | {location}
      </div>

      <div className="spread-ou">O/U: {overUnder}</div>

      <div className="pick-buttons">
        <button
          className={`pick-button ${isAwaySelected ? "selected" : ""}`}
          onClick={() => onPick(matchup, matchup.awayFranchiseSeasonId)}
          disabled={isLocked}
        >
          {isAwaySelected && <FaCheckCircle className="pick-check-icon" />}
          {!isAwaySelected && isLocked && <FaLock className="pick-lock-icon" />}
          {matchup.awayShort}
        </button>

        <button
          className="insight-button"
          onClick={() => onViewInsight(matchup)}
          disabled={
            !matchup.isPreviewAvailable || !isInsightUnlocked || isLocked
          }
          title={
            !matchup.isPreviewAvailable
              ? "Preview not available"
              : isInsightUnlocked
              ? isLocked
                ? "Locked – game has started"
                : "View Insight"
              : "Unlock Insights with Subscription"
          }
        >
          {isInsightUnlocked ? <FaChartLine /> : <FaLock />}
        </button>

        <button
          className={`pick-button ${isHomeSelected ? "selected" : ""}`}
          onClick={() => onPick(matchup, matchup.homeFranchiseSeasonId)}
          disabled={isLocked}
        >
          {isHomeSelected && <FaCheckCircle className="pick-check-icon" />}
          {!isHomeSelected && isLocked && <FaLock className="pick-lock-icon" />}
          {matchup.homeShort}
        </button>
      </div>
    </div>
  );
}

export default MatchupCard;
