import React from "react";
import "./ContestOverview.css";

export default function ContestOverviewPlaylog({ playLog }) {
  return (
    <div className="contest-section">
      <div className="contest-section-title">Play Log</div>
      <div className="contest-scoring-summary-section">
        <div className="contest-scoring-summary-list">
          {playLog && playLog.length > 0 ? (
            playLog.map((play, idx) => (
              <div key={idx} className="contest-scoring-summary-item">
                <span className="contest-scoring-summary-quarter">Q{play.quarter}</span>
                <span className="contest-scoring-summary-team">{play.team}</span>
                <span className="contest-scoring-summary-desc">{play.description}</span>
                <span className="contest-scoring-summary-time">{play.timeRemaining}</span>
              </div>
            ))
          ) : (
            <div className="contest-scoring-summary-item">No play log available.</div>
          )}
        </div>
      </div>
    </div>
  );
}
