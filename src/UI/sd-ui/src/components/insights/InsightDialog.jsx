import React, { useEffect } from "react";
import "./InsightDialog.css";
// import teams from "../../data/teams";

function InsightDialog({ isOpen, onClose, matchup, loading }) {
  useEffect(() => {
    if (isOpen) {
      document.body.classList.add("modal-open");
    } else {
      document.body.classList.remove("modal-open");
    }

    return () => document.body.classList.remove("modal-open");
  }, [isOpen]);

  if (!isOpen || !matchup) return null;

  return (
    <div className="insight-dialog-overlay">
      <div className="insight-dialog" onClick={(e) => e.stopPropagation()}>
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
              alt={`${matchup.home} logo`}
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