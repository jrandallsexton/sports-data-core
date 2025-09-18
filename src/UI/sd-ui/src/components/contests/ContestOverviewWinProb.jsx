import React from "react";
import {
  LineChart, Line, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer, CartesianGrid
} from "recharts";
import "./ContestOverview.css";

export default function ContestOverviewWinProb({ winProbability }) {
  return (
    <div className="contest-section">
      <div className="contest-section-title">Win Probability</div>
      <div className="contest-winprob-section">
        <div className="contest-winprob-chart-wrapper">
          <ResponsiveContainer width="100%" height={220}>
            <LineChart data={winProbability.points.map(pt => ({
              quarter: `Q${pt.quarter}`,
              clock: pt.gameClock,
              Home: pt.homeWinPercent,
              Away: pt.awayWinPercent
            }))} margin={{ top: 20, right: 30, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="quarter" />
              <YAxis domain={[0, 100]} tickFormatter={v => `${v}%`} />
              <Tooltip formatter={v => `${v}%`} />
              <Legend />
              <Line type="monotone" dataKey="Home" stroke="#61dafb" strokeWidth={3} dot={{ r: 4 }} />
              <Line type="monotone" dataKey="Away" stroke="#e57373" strokeWidth={3} dot={{ r: 4 }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
        <div className="contest-winprob-list">
          {winProbability.points.map((pt, idx) => (
            <div key={idx} className="contest-winprob-item">
              <span className="contest-winprob-quarter">Q{pt.quarter}</span>
              <span className="contest-winprob-clock">{pt.gameClock}</span>
              <span className="contest-winprob-home">Home: {pt.homeWinPercent}%</span>
              <span className="contest-winprob-away">Away: {pt.awayWinPercent}%</span>
            </div>
          ))}
          <div className="contest-winprob-final">
            <span>Final: </span>
            <span className="contest-winprob-home">Home: {winProbability.finalHomeWinPercent}%</span>
            <span className="contest-winprob-away">Away: {winProbability.finalAwayWinPercent}%</span>
          </div>
        </div>
      </div>
    </div>
  );
}
