import "./MatchupCard.css";
import { FaChartLine, FaLock } from "react-icons/fa";
import { Link } from "react-router-dom";

function MatchupCard({
  matchup,
  userPick,
  onPick,
  onViewInsight,
  isInsightUnlocked,
}) {
  const awaySpread = matchup.awaySpread ?? 0;
  const homeSpread = matchup.homeSpread ?? 0;
  const overUnder = matchup.overUnder ?? "TBD";
  const gameTime = new Date(matchup.startDateUtc).toLocaleString();
  const venue = matchup.venue ?? "TBD";
  const location = `${matchup.venueCity ?? ""}, ${matchup.venueState ?? ""}`;
  const seasonYear = matchup.seasonYear; // required for deep links

  return (
    <div className="matchup-card">
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
                Conference: {matchup.awayConferenceWins}-{matchup.awayConferenceLosses}
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
                Conference: {matchup.homeConferenceWins}-{matchup.homeConferenceLosses}
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
          className={`pick-button ${
            userPick === matchup.away ? "selected" : ""
          }`}
          onClick={() => onPick(matchup.contestId, matchup.away)}
        >
          {matchup.away}
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
            userPick === matchup.home ? "selected" : ""
          }`}
          onClick={() => onPick(matchup.contestId, matchup.home)}
        >
          {matchup.home}
        </button>
      </div>
    </div>
  );
}

export default MatchupCard;
