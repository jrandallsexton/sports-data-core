
import React from "react";
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer
} from "recharts";
import "./ContestOverview.css";

// Custom tooltip for dark theme
const CustomTooltip = ({ active, payload, label }) => {
  if (active && payload && payload.length) {
    return (
      <div style={{
        background: "#23272f",
        color: "#f8f9fa",
        border: "1px solid #444",
        borderRadius: 6,
        padding: "8px 12px",
        fontSize: 14,
        boxShadow: "0 2px 8px rgba(0,0,0,0.3)"
      }}>
        <div>{label}</div>
        <div>Home: {payload[0]?.value}%</div>
        <div>Away: {payload[1]?.value}%</div>
      </div>
    );
  }
  return null;
};

export default function ContestOverviewWinProb({ winProbability, homeTeam, awayTeam }) {
  // Debug: log the team colors

  // Use fixed colors for chart and legend
  const HOME_COLOR = '#4FC3F7'; // Light blue used in other CSS files
  const AWAY_COLOR = 'yellow';

  const chartData = winProbability.points.map(pt => ({
    quarter: pt.quarter,
    clock: pt.gameClock,
    Home: pt.homeWinPercent,
    Away: pt.awayWinPercent
  }));

  // Get unique quarters for X axis ticks
  const uniqueQuarters = Array.from(new Set(chartData.map(d => d.quarter)));

  return (
    <div className="contest-section" style={{ padding: "12px 10px 10px 10px", margin: 0 }}>
      <div className="contest-section-title" style={{ marginBottom: 8 }}>Win Probability</div>
      <div className="contest-winprob-section" style={{ padding: 0, margin: 0 }}>
        <div className="contest-winprob-chart-wrapper" style={{ padding: 0, margin: 0 }}>
          <ResponsiveContainer width="100%" height={180}>
            <AreaChart data={chartData} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                dataKey="quarter"
                ticks={uniqueQuarters}
                tickFormatter={q => `Q${q}`}
                interval={0}
              />
              <YAxis domain={[0, 100]} tickFormatter={v => `${v}%`} />
              <Tooltip content={<CustomTooltip />} />
              <Legend
                payload={[
                  {
                    value: 'Home',
                    type: 'line',
                    color: HOME_COLOR,
                  },
                  {
                    value: 'Away',
                    type: 'line',
                    color: AWAY_COLOR,
                  },
                ]}
                formatter={(value, entry) => (
                  <span style={{ color: entry.color, fontWeight: 600 }}>{value}</span>
                )}
              />
              {/* Remove gradients, use solid fill for color visibility */}
              <Area
                type="monotone"
                dataKey="Home"
                stroke={HOME_COLOR}
                fill={HOME_COLOR}
                fillOpacity={0.18}
                name="Home"
                dot={false}
                strokeWidth={3}
                activeDot={{ r: 5, fill: HOME_COLOR, stroke: '#fff', strokeWidth: 2 }}
              />
              <Area
                type="monotone"
                dataKey="Away"
                stroke={AWAY_COLOR}
                fill={AWAY_COLOR}
                fillOpacity={0.18}
                name="Away"
                dot={false}
                strokeWidth={3}
                activeDot={{ r: 5, fill: AWAY_COLOR, stroke: '#fff', strokeWidth: 2 }}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
}
