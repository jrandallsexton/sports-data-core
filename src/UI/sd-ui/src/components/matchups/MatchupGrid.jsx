import "./MatchupGrid.css";
import { FaChartLine, FaLock, FaSpinner } from "react-icons/fa";
import { Link } from "react-router-dom";
import HelmetLogo from "./HelmetLogo";

function MatchupGrid({
  matchups,
  loading,
  userPicks,
  onPick,
  onViewInsight,
  isSubscribed,
}) {
  if (loading) {
    return (
      <div style={{ textAlign: "center", marginTop: "40px" }}>
        <FaSpinner className="spinner" style={{ fontSize: "2rem" }} />
        Loading Matchups...
      </div>
    );
  }

  return (
    <div className="matchup-grid">
      {/* Grid Header */}
      <div className="grid-row grid-header">
        <div>Game</div>
        <div>Time</div>
        <div>Location</div>
        <div>Spread</div>
        <div>O/U</div>
        <div>Pick</div>
        <div>Consensus</div>
        <div>Insight</div>
      </div>

      {matchups.map((matchup, index) => {
        const gameTime = new Date(matchup.startDateUtc).toLocaleString();
        const location = `${matchup.venueCity ?? ""}, ${matchup.venueState ?? ""}`;
        const spread = matchup.awaySpread > 0 ? `+${matchup.awaySpread}` : matchup.awaySpread;
        const overUnder = matchup.overUnder ?? "TBD";

        return (
          <div
            key={matchup.id}
            className={`grid-row ${userPicks[matchup.id] ? "pick-selected" : ""}`}
          >
            {/* Game */}
            <div className="grid-cell game-cell">
              <div className="team-entry">
                <HelmetLogo logoUrl={matchup.awayLogoUri} flip />
                <div className="team">
                  {matchup.awayRank && (
                    <span className="team-ranking">#{matchup.awayRank} </span>
                  )}
                  <Link
                    to={`/app/sport/football/ncaa/team/${matchup.awayTeamSlug}/${matchup.seasonYear}`}
                    className="team-link"
                  >
                    {matchup.away}
                  </Link>
                </div>
              </div>
              <div style={{ fontSize: "0.8rem", opacity: 0.7 }}>at</div>
              <div className="team-entry">
                <HelmetLogo logoUrl={matchup.homeLogoUri} />
                <div className="team">
                  {matchup.homeRank && (
                    <span className="team-ranking">#{matchup.homeRank} </span>
                  )}
                  <Link
                    to={`/app/sport/football/ncaa/team/${matchup.homeTeamSlug}/${matchup.seasonYear}`}
                    className="team-link"
                  >
                    {matchup.home}
                  </Link>
                </div>
              </div>
            </div>

            {/* Time */}
            <div className="grid-cell">
              <div className="game-time" style={{ fontSize: "0.9rem" }}>
                {gameTime}
              </div>
            </div>

            {/* Location */}
            <div className="grid-cell">
              <div className="location" style={{ fontSize: "0.8rem", opacity: 0.8 }}>
                {matchup.venue}
              </div>
              <div className="location-city" style={{ fontSize: "0.7rem", opacity: 0.6 }}>
                {location}
              </div>
            </div>

            {/* Spread */}
            <div className="grid-cell">
              <div className="spread" style={{ fontSize: "0.9rem" }}>
                {spread}
              </div>
            </div>

            {/* Over/Under */}
            <div className="grid-cell">
              <div className="over-under" style={{ fontSize: "0.9rem" }}>
                {overUnder}
              </div>
            </div>

            {/* Pick */}
            <div className="grid-cell">
              <div className="grid-pick-options">
                <label>
                  <input
                    type="radio"
                    name={`pick-${matchup.id}`}
                    value={matchup.away}
                    checked={userPicks[matchup.id] === matchup.away}
                    onChange={() => onPick(matchup.id, matchup.away)}
                  />
                  <span className="team">{matchup.away}</span>
                </label>
                <label>
                  <input
                    type="radio"
                    name={`pick-${matchup.id}`}
                    value={matchup.home}
                    checked={userPicks[matchup.id] === matchup.home}
                    onChange={() => onPick(matchup.id, matchup.home)}
                  />
                  <span className="team">{matchup.home}</span>
                </label>
              </div>
            </div>

            {/* Consensus */}
            <div className="grid-cell">
              <div className="consensus" style={{ fontSize: "0.9rem", color: "#bbb" }}>
                {matchup.awayPickPercent ?? 0}% / {matchup.homePickPercent ?? 0}%
              </div>
            </div>

            {/* Insight */}
            <div className="grid-cell">
              <button
                onClick={() => onViewInsight(matchup)}
                disabled={!(isSubscribed || index === 0)}
                title={
                  isSubscribed || index === 0
                    ? "View Insight"
                    : "Unlock Insights with Subscription"
                }
                style={{
                  background: "none",
                  border: "none",
                  color: "#61dafb",
                  cursor: isSubscribed || index === 0 ? "pointer" : "not-allowed",
                  fontSize: "1.4rem",
                }}
              >
                {isSubscribed || index === 0 ? <FaChartLine /> : <FaLock />}
              </button>
            </div>
          </div>
        );
      })}
    </div>
  );
}

export default MatchupGrid;
