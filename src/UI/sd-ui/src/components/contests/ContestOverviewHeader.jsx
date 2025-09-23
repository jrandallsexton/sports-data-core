import React from "react";
import "./ContestOverview.css";

export default function ContestOverviewHeader({ homeTeam, awayTeam, quarterScores }) {
  const awayTotal = quarterScores.reduce((sum, q) => sum + q.awayScore, 0);
  const homeTotal = quarterScores.reduce((sum, q) => sum + q.homeScore, 0);

  return (
    <div className="contest-section">
      <div className="contest-header-row contest-header-flex">
        {/* Away Side */}
        <img src={awayTeam.logoUrl} alt={awayTeam.displayName} className="contest-team-logo" />
        <div className="contest-team-name contest-header-team-name">{awayTeam.displayName}</div>
        <div className="contest-team-score contest-header-team-score-away">{awayTotal}</div>
        {/* Box Score Table */}
        <div className="contest-boxscore-table-wrapper compact">
          <div className="contest-boxscore-final">Final</div>
          <table className="contest-boxscore-table compact">
            <thead>
              <tr>
                <th></th>
                {quarterScores.map(q => (
                  <th key={q.quarter}>{q.quarter}</th>
                ))}
                <th>T</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td className="contest-boxscore-team-short">{awayTeam.displayName.split(' ')[0].toUpperCase()}</td>
                {quarterScores.map(q => (
                  <td key={q.quarter}>{q.awayScore}</td>
                ))}
                <td className="contest-boxscore-total">{awayTotal}</td>
              </tr>
              <tr>
                <td className="contest-boxscore-team-short">{homeTeam.displayName.split(' ')[0].toUpperCase()}</td>
                {quarterScores.map(q => (
                  <td key={q.quarter}>{q.homeScore}</td>
                ))}
                <td className="contest-boxscore-total">{homeTotal}</td>
              </tr>
            </tbody>
          </table>
        </div>
        {/* Home Side */}
        <div className="contest-team-score contest-header-team-score-home">{homeTotal}</div>
        <div className="contest-team-name contest-header-team-name">{homeTeam.displayName}</div>
        <img src={homeTeam.logoUrl} alt={homeTeam.displayName} className="contest-team-logo" />
      </div>
    </div>
  );
}
