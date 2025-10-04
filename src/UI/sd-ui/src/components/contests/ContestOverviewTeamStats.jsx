import React from "react";
import "./ContestOverview.css";

export default function ContestOverviewTeamStats({ homeTeam, awayTeam, teamStats }) {
  const awayStats = teamStats?.awayTeam?.stats || {};
  const homeStats = teamStats?.homeTeam?.stats || {};
  return (
    <div className="contest-section">
      <div className="contest-section-title">Team Stats</div>
      <div className="contest-teamstats-section">
        <div className="contest-teamstats-row">
          <div className="contest-teamstats-team contest-teamstats-away">
            <div className="contest-teamstats-team-name">{awayTeam.displayName}</div>
            <ul className="contest-teamstats-list">
              {Object.keys(awayStats).length > 0 ? (
                Object.entries(awayStats).map(([stat, value]) => (
                  <li key={stat} className="contest-teamstats-item">
                    <span className="contest-teamstats-stat-name">{stat}:</span> <span className="contest-teamstats-stat-value">{value}</span>
                  </li>
                ))
              ) : (
                <li className="contest-teamstats-item">No stats available.</li>
              )}
            </ul>
          </div>
          <div className="contest-teamstats-team contest-teamstats-home">
            <div className="contest-teamstats-team-name">{homeTeam.displayName}</div>
            <ul className="contest-teamstats-list">
              {Object.keys(homeStats).length > 0 ? (
                Object.entries(homeStats).map(([stat, value]) => (
                  <li key={stat} className="contest-teamstats-item">
                    <span className="contest-teamstats-stat-name">{stat}:</span> <span className="contest-teamstats-stat-value">{value}</span>
                  </li>
                ))
              ) : (
                <li className="contest-teamstats-item">No stats available.</li>
              )}
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
