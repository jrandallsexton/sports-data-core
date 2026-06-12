import "./ResultsPage.css";

function pickIndicator(hit) {
  if (hit === true) return { symbol: "✓", className: "hit" };
  if (hit === false) return { symbol: "✗", className: "miss" };
  return { symbol: "—", className: "ungraded" };
}

function teamRowClass(franchiseSeasonId, predictedSU) {
  if (predictedSU && franchiseSeasonId === predictedSU) return "team-row picked";
  return "team-row";
}

export default function ScorebugTile({ game }) {
  const su = pickIndicator(game.suHit);
  const ats = pickIndicator(game.atsHit);

  return (
    <div className="scorebug-tile" title={`Spread: ${game.spread ?? "—"}`}>
      <div className={teamRowClass(game.awayFranchiseSeasonId, game.predictedStraightUpWinner)}>
        <span className="team-name">{game.awayShort}</span>
        <span className="team-score">{game.awayScore ?? "—"}</span>
      </div>
      <div className={teamRowClass(game.homeFranchiseSeasonId, game.predictedStraightUpWinner)}>
        <span className="team-name">{game.homeShort}</span>
        <span className="team-score">{game.homeScore ?? "—"}</span>
      </div>

      <div className="tile-divider" />

      <div className="picks-row">
        <span className={`pick-badge ${su.className}`}>SU {su.symbol}</span>
        <span className={`pick-badge ${ats.className}`}>ATS {ats.symbol}</span>
      </div>
    </div>
  );
}
