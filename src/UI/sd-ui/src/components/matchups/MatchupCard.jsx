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
      {/* Away Team Row */}
      <div className="team-row">
        <div className="team-info">
          {awayTeamInfo && (
            <img
              src={awayTeamInfo.logoUrl}
              alt={`${matchup.awayTeam} logo`}
              className="matchup-logo"
            />
          )}
          <div className="team-details">
            <div className="team-name-row">
              {awayTeamInfo?.ranking && (
                <span className="team-ranking">#{awayTeamInfo.ranking}</span>
              )}
              <Link to={`/app/sport/football/ncaa/team/${awayTeamInfo?.slug}`} className="team-link">
                {matchup.awayTeam}
              </Link>
            </div>
            <div className="team-record">
              <span>Overall: {awayTeamInfo?.overallRecord || "TBD"}</span>
              <span>Conference: {awayTeamInfo?.conferenceRecord || "TBD"}</span>
            </div>
          </div>
        </div>
        <div className="team-spread">
          {matchup.spread.startsWith("-")
            ? `+${Math.abs(parseFloat(matchup.spread))}`
            : `+${matchup.spread}`}
        </div>
      </div>

      <div className="at-divider">at</div>

      {/* Home Team Row */}
      <div className="team-row">
        <div className="team-info">
          {homeTeamInfo && (
            <img
              src={homeTeamInfo.logoUrl}
              alt={`${matchup.homeTeam} logo`}
              className="matchup-logo"
            />
          )}
          <div className="team-details">
            <div className="team-name-row">
              {homeTeamInfo?.ranking && (
                <span className="team-ranking">#{homeTeamInfo.ranking}</span>
              )}
              <Link to={`/app/sport/football/ncaa/team/${homeTeamInfo?.slug}`} className="team-link">
                {matchup.homeTeam}
              </Link>
            </div>
            <div className="team-record">
              <span>Overall: {homeTeamInfo?.overallRecord || "TBD"}</span>
              <span>Conference: {homeTeamInfo?.conferenceRecord || "TBD"}</span>
            </div>
          </div>
        </div>
        <div className="team-spread">{matchup.spread}</div>
      </div>

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

      <div className="spread-ou">O/U: {matchup.overUnder}</div>

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
