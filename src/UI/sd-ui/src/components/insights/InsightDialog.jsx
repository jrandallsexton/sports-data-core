import "./InsightDialog.css";
// import teams from "../../data/teams";

function InsightDialog({ isOpen, onClose, matchup, loading }) {
  console.log("InsightDialog visible:", isOpen, matchup);

  if (!isOpen || !matchup) return null;

  // const awayTeamInfo = teams[matchup.away];
  // const homeTeamInfo = teams[matchup.home];

  return (
    <div className="insight-dialog-overlay">
      <div className="insight-dialog">
        <button className="close-x-button" onClick={onClose}>
          &times;
        </button>

        <div className="helmet-row">
          {matchup.awayLogoUri && (
            <img
              src={matchup.awayLogoUri}
              alt={`${matchup.away} logo`}
              className="matchup-logo"
            />
          )}
          <h2>
            {matchup.away}
            <br />@<br />
            {matchup.home}
          </h2>
          {matchup.homeLogoUri && (
            <img
              src={matchup.homeLogoUri}
              alt={`${matchup.homeTeam} logo`}
              className="matchup-logo"
            />
          )}
        </div>
        <hr className="divider" />

        <div className="insight-text">
          {loading ? (
            <div className="spinner"></div>
          ) : (
            <div className="insight-text-loaded">
              <div className="overview-section">
                <h3>Overview</h3>
                <p>{matchup.insightText || "Overview not available."}</p>
              </div>

              <hr className="divider" />

              <div className="analysis-section">
                <h3>Analysis</h3>
                <p>{matchup.analysis || "Analysis not available."}</p>
              </div>

              <hr className="divider" />

              <div className="prediction-section">
                <h3>
                  sportDeets<span className="tm-symbol">™</span> Prediction
                </h3>
                <p className="prediction-animated">
                  {matchup.prediction || "Prediction not available."}
                </p>
              </div>
            </div>
          )}
        </div>

        <button className="close-button" onClick={onClose}>
          Close
        </button>
      </div>
    </div>
  );
}

export default InsightDialog;
