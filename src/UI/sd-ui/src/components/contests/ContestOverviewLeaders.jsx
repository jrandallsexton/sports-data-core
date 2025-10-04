import React from "react";
import "./ContestOverview.css";

export default function ContestOverviewLeaders({ homeTeam, awayTeam, leaders }) {
  const awayLeaders = leaders?.awayLeaders || [];
  const homeLeaders = leaders?.homeLeaders || [];
  return (
    <div className="contest-section">
      <div className="contest-section-title">Leaders</div>
      <div className="contest-leaders-section">
        <div className="contest-leaders-row">
          {/* Away Leaders */}
          <div className="contest-leaders-team contest-leaders-away">
            <div className="contest-leaders-team-name">{awayTeam.displayName}</div>
            {awayLeaders.length > 0 ? (
              awayLeaders.map((l, idx) => (
                <div key={idx} className="contest-leader-item">
                  <span className="contest-leader-category">{l.category}:</span> <span className="contest-leader-player">{l.playerName}</span>
                  <div className="contest-leader-statline">{l.statLine}</div>
                </div>
              ))
            ) : (
              <div className="contest-leader-item">No leaders available.</div>
            )}
          </div>
          {/* Home Leaders */}
          <div className="contest-leaders-team contest-leaders-home">
            <div className="contest-leaders-team-name">{homeTeam.displayName}</div>
            {homeLeaders.length > 0 ? (
              homeLeaders.map((l, idx) => (
                <div key={idx} className="contest-leader-item">
                  <span className="contest-leader-category">{l.category}:</span> <span className="contest-leader-player">{l.playerName}</span>
                  <div className="contest-leader-statline">{l.statLine}</div>
                </div>
              ))
            ) : (
              <div className="contest-leader-item">No leaders available.</div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
