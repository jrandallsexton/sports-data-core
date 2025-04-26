import "./MatchupCard.css";

function MatchupCard({ matchup, userPick, onPick }) {
  return (
    <div className="matchup-card">
      <div className="teams">
        <div className="team">
          {matchup.awayTeam} (
          {matchup.spread.startsWith("-")
            ? `+${Math.abs(parseFloat(matchup.spread))}`
            : `+${matchup.spread}`}
          )
        </div>
        <div>at</div>
        <div className="team">
          {matchup.homeTeam} ({matchup.spread})
        </div>
      </div>

      <div className="spread-ou">
        O/U: {matchup.overUnder}
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
