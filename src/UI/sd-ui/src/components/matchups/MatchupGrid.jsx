import "./MatchupGrid.css";
import { FaChartLine, FaLock, FaSpinner } from "react-icons/fa";
import teams from "../../data/teams"; // ✅ Import teams lookup!

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
        const awayShortName =
          teams[matchup.awayTeam]?.shortName || matchup.awayTeam;
        const homeShortName =
          teams[matchup.homeTeam]?.shortName || matchup.homeTeam;

        return (
          <div
            key={matchup.id}
            className={`grid-row ${
              userPicks[matchup.id] ? "pick-selected" : ""
            }`}
          >
            {/* Game */}
            <div className="grid-cell">
              <div className="team">{matchup.awayTeam}</div>
              <div style={{ fontSize: "0.8rem", opacity: 0.7 }}>at</div>
              <div className="team">{matchup.homeTeam}</div>
            </div>

            {/* Time */}
            <div className="grid-cell">
              <div className="game-time" style={{ fontSize: "0.9rem" }}>
                {matchup.gameTime}
              </div>
            </div>

            {/* Location */}
            <div className="grid-cell">
              <div
                className="location"
                style={{ fontSize: "0.8rem", opacity: 0.8 }}
              >
                {matchup.stadium}
              </div>
              <div
                className="location-city"
                style={{ fontSize: "0.7rem", opacity: 0.6 }}
              >
                {matchup.location}
              </div>
            </div>

            {/* Spread */}
            <div className="grid-cell">
              <div className="spread" style={{ fontSize: "0.9rem" }}>
                {matchup.spread}
              </div>
            </div>

            {/* Over/Under */}
            <div className="grid-cell">
              <div className="over-under" style={{ fontSize: "0.9rem" }}>
                {matchup.overUnder}
              </div>
            </div>

            {/* Pick */}
            <div className="grid-cell">
              <div className="grid-pick-options">
                <label>
                  <input
                    type="radio"
                    name={`pick-${matchup.id}`}
                    value={matchup.awayTeam}
                    checked={userPicks[matchup.id] === matchup.awayTeam}
                    onChange={() => onPick(matchup.id, matchup.awayTeam)}
                  />
                  <span className="team">{awayShortName}</span>
                </label>
                <label>
                  <input
                    type="radio"
                    name={`pick-${matchup.id}`}
                    value={matchup.homeTeam}
                    checked={userPicks[matchup.id] === matchup.homeTeam}
                    onChange={() => onPick(matchup.id, matchup.homeTeam)}
                  />
                  <span className="team">{homeShortName}</span>
                </label>
              </div>
            </div>

            {/* Consensus */}
            <div className="grid-cell">
              <div
                className="consensus"
                style={{ fontSize: "0.9rem", color: "#bbb" }}
              >
                {matchup.awayPickPercent}% / {matchup.homePickPercent}%
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
                  cursor:
                    isSubscribed || index === 0 ? "pointer" : "not-allowed",
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
