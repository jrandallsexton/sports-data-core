import React from "react";
import "./ContestOverview.css";

export default function ContestOverviewAnalysis({ matchupAnalysis }) {
  if (!matchupAnalysis || typeof matchupAnalysis !== "object") {
    return (
      <div className="contest-section">
        <div className="contest-section-title">Matchup Analysis</div>
        <div className="contest-analysis-section">No analysis available.</div>
      </div>
    );
  }
  return (
    <div className="contest-section">
      <div className="contest-section-title">Matchup Analysis</div>
      <div className="contest-analysis-section">
        <div className="contest-analysis-item">
          <span className="contest-analysis-label">Predicted:</span>{" "}
          {matchupAnalysis.predictedSummary}
        </div>
        <div className="contest-analysis-item">
          <span className="contest-analysis-label">Actual:</span>{" "}
          {matchupAnalysis.actualResultSummary}
        </div>
        <div className="contest-analysis-item">
          <span className="contest-analysis-label">Model Accuracy:</span>{" "}
          {matchupAnalysis.modelAccuracyNotes}
        </div>
        <div className="contest-analysis-item">
          <span className="contest-analysis-label">Where It Was Right:</span>{" "}
          {matchupAnalysis.whereItWasRight}
        </div>
        <div className="contest-analysis-item">
          <span className="contest-analysis-label">Where It Was Wrong:</span>{" "}
          {matchupAnalysis.whereItWasWrong}
        </div>
      </div>
    </div>
  );
}
