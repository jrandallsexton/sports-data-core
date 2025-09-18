import React from "react";
import "./ContestOverview.css";

export default function ContestOverviewLeaders({ homeTeam, awayTeam, leaders }) {
  return (
    <div className="contest-section">
      <div className="contest-section-title">Leaders</div>
      <div className="contest-leaders-section">
        <div className="contest-leaders-row">
          {/* Away Leaders */}
          <div className="contest-leaders-team contest-leaders-away">
            <div className="contest-leaders-team-name">{awayTeam.displayName}</div>
            {leaders.awayLeaders.map((l, idx) => (
              <div key={idx} className="contest-leader-item">
                <span className="contest-leader-category">{l.category}:</span> <span className="contest-leader-player">{l.playerName}</span>
                <div className="contest-leader-statline">{l.statLine}</div>
              </div>
            ))}
          </div>
          {/* Home Leaders */}
          <div className="contest-leaders-team contest-leaders-home">
            <div className="contest-leaders-team-name">{homeTeam.displayName}</div>
            {leaders.homeLeaders.map((l, idx) => (
              <div key={idx} className="contest-leader-item">
                <span className="contest-leader-category">{l.category}:</span> <span className="contest-leader-player">{l.playerName}</span>
                <div className="contest-leader-statline">{l.statLine}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
