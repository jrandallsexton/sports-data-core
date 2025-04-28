import "./InsightDialog.css";
import { FaExternalLinkAlt } from "react-icons/fa";
import teams from "../../data/teams";

function InsightDialog({
  isOpen,
  onClose,
  matchup,
  bullets,
  prediction,
  loading,
}) {
  if (!isOpen || !matchup) return null;

  const awayTeamInfo = teams[matchup.awayTeam];
  const homeTeamInfo = teams[matchup.homeTeam];

  return (
    <div className="insight-dialog-overlay">
      <div className="insight-dialog">
        <button className="close-x-button" onClick={onClose}>
          &times;
        </button>

        <div className="helmet-row">
          {awayTeamInfo && (
            <img
              src={awayTeamInfo.logoUrl}
              alt={`${matchup.awayTeam} logo`}
              className="helmet-logo away-logo"
            />
          )}
          <h2>
            {matchup.awayTeam} vs {matchup.homeTeam}
          </h2>
          {homeTeamInfo && (
            <img
              src={homeTeamInfo.logoUrl}
              alt={`${matchup.homeTeam} logo`}
              className="helmet-logo home-logo"
            />
          )}
        </div>

        <div className="insight-text">
          {loading ? (
            <div className="spinner"></div>
          ) : (
            <div className="insight-text-loaded">
              <div className="analysis-section">
                <h3>Analysis</h3>
                <ul>
                  {bullets.map((bullet, idx) => (
                    <li key={idx}>
                      {bullet.text}
                      {bullet.link && (
                        <a
                          href={bullet.link}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="bullet-link-icon"
                          title="View Evidence"
                        >
                          <FaExternalLinkAlt />
                        </a>
                      )}
                    </li>
                  ))}
                </ul>
              </div>

              <hr className="divider" />

              <div className="prediction-section">
                <h3>sportDeets Prediction</h3>
                <p className="prediction-animated">
                  {prediction || "Prediction not available."}
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
