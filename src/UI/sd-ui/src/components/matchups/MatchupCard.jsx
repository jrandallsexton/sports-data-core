import "./MatchupCard.css";
import { FaChartLine, FaLock } from "react-icons/fa";
import teams from "../../data/teams";
import { Link } from "react-router-dom";

function MatchupCard({
  matchup,
  userPick,
  onPick,
  onViewInsight,
  isInsightUnlocked,
}) {
  const awayTeamInfo = Object.values(teams).find(
    (t) => t.name?.toLowerCase() === matchup.awayTeam.toLowerCase()
  );
  const homeTeamInfo = Object.values(teams).find(
    (t) => t.name?.toLowerCase() === matchup.homeTeam.toLowerCase()
  );

  return (
    <div className="matchup-card">
      <div className="teams">
        <div className="team">
          {awayTeamInfo && (
            <img
              src={awayTeamInfo.logoUrl}
              alt={`${matchup.awayTeam} logo`}
              className="matchup-logo"
            />
          )}
          <Link to={`/app/team/${awayTeamInfo?.slug}`} className="team-link">
            {matchup.awayTeam}
          </Link>{" "}
          (
          {matchup.spread.startsWith("-")
            ? `+${Math.abs(parseFloat(matchup.spread))}`
            : `+${matchup.spread}`}
          )
        </div>

        <div>at</div>

        <div className="team">
          {homeTeamInfo && (
            <img
              src={homeTeamInfo.logoUrl}
              alt={`${matchup.homeTeam} logo`}
              className="matchup-logo"
            />
          )}
          <Link to={`/app/team/${homeTeamInfo?.slug}`} className="team-link">
            {matchup.homeTeam}
          </Link>{" "}
          ({matchup.spread})
        </div>
      </div>

      <div className="spread-ou">O/U: {matchup.overUnder}</div>

      <div className="game-time-location">
        {matchup.gameTime} | {matchup.stadium} | {matchup.location}
      </div>

      <div className="pick-distribution">
        <div className="distribution-bar">
          <div
            className="distribution-fill away"
            style={{ width: `${matchup.awayPickPercent}%` }}
          />
          <div
            className="distribution-fill home"
            style={{ width: `${matchup.homePickPercent}%` }}
          />
        </div>
        <div className="distribution-text">
          {matchup.awayPickPercent}% {matchup.awayTeam} |{" "}
          {matchup.homePickPercent}% {matchup.homeTeam}
        </div>
      </div>

      <div className="spread-ou">
        Spread: {matchup.spread} | O/U: {matchup.overUnder}
      </div>

      <div className="pick-buttons">
        <button
          className={`pick-button ${
            userPick === matchup.awayTeam ? "selected" : ""
          }`}
          onClick={() => onPick(matchup.id, matchup.awayTeam)}
        >
          {matchup.awayTeam}
        </button>

        <button
          className="insight-button"
          onClick={() => onViewInsight(matchup)}
          disabled={!isInsightUnlocked}
          title={
            isInsightUnlocked
              ? "View Insight"
              : "Unlock Insights with Subscription"
          }
        >
          {isInsightUnlocked ? <FaChartLine /> : <FaLock />}
        </button>

        <button
          className={`pick-button ${
            userPick === matchup.homeTeam ? "selected" : ""
          }`}
          onClick={() => onPick(matchup.id, matchup.homeTeam)}
        >
          {matchup.homeTeam}
        </button>
      </div>
    </div>
  );
}

export default MatchupCard;
