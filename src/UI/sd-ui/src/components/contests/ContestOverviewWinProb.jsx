import React from "react";
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer
} from "recharts";
import "./ContestOverview.css";

function getGradientId(teamKey) {
  return `${teamKey}-color-gradient`;
}

export default function ContestOverviewWinProb({ winProbability, homeTeam, awayTeam }) {
  const chartData = winProbability.points.map(pt => ({
    quarter: `Q${pt.quarter}`,
    clock: pt.gameClock,
    Home: pt.homeWinPercent,
    Away: pt.awayWinPercent
  }));

  return (
    <div className="contest-section">
      <div className="contest-section-title">Win Probability</div>
      <div className="contest-winprob-section">
        <div className="contest-winprob-chart-wrapper">
          <ResponsiveContainer width="100%" height={220}>
            <AreaChart data={chartData} margin={{ top: 20, right: 30, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="quarter" />
              <YAxis domain={[0, 100]} tickFormatter={v => `${v}%`} />
              <Tooltip formatter={v => `${v}%`} />
              <Legend />
              <defs>
                <linearGradient id={getGradientId('home')} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={homeTeam.colorPrimary} stopOpacity={0.7} />
                  <stop offset="100%" stopColor={homeTeam.colorPrimary} stopOpacity={0.2} />
                </linearGradient>
                <linearGradient id={getGradientId('away')} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={awayTeam.colorPrimary} stopOpacity={0.7} />
                  <stop offset="100%" stopColor={awayTeam.colorPrimary} stopOpacity={0.2} />
                </linearGradient>
              </defs>
              <Area type="monotone" dataKey="Home" stroke={homeTeam.colorPrimary} fill={`url(#${getGradientId('home')})`} />
              <Area type="monotone" dataKey="Away" stroke={awayTeam.colorPrimary} fill={`url(#${getGradientId('away')})`} />
            </AreaChart>
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
