import React from 'react';
import './ContestOverview.css';

function pct(v) {
  if (v === null || v === undefined) return '-';
  return `${(Number(v) * 100).toFixed(1)}%`;
}

function num(v, digits = 2) {
  if (v === null || v === undefined) return '-';
  return Number(v).toFixed(digits);
}

function MetricsCard({ title, metrics }) {
  if (!metrics) return null;
  return (
    <div className="contest-section" style={{ padding: 12 }}>
      <div className="contest-section-title">{title}</div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">YPP</span><span className="contest-teamstats-stat-value">{num(metrics.ypp)}</span></div>
        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Opp YPP</span><span className="contest-teamstats-stat-value">{num(metrics.oppYpp)}</span></div>

        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Success Rate</span><span className="contest-teamstats-stat-value">{pct(metrics.successRate)}</span></div>
        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Opp Success</span><span className="contest-teamstats-stat-value">{pct(metrics.oppSuccessRate)}</span></div>

        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Explosive Rate</span><span className="contest-teamstats-stat-value">{pct(metrics.explosiveRate)}</span></div>
        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Opp Explosive</span><span className="contest-teamstats-stat-value">{pct(metrics.oppExplosiveRate)}</span></div>

        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Points / Drive</span><span className="contest-teamstats-stat-value">{num(metrics.pointsPerDrive)}</span></div>
        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Opp Points / Drive</span><span className="contest-teamstats-stat-value">{num(metrics.oppPointsPerDrive)}</span></div>

        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Third/Fourth Conv</span><span className="contest-teamstats-stat-value">{pct(metrics.thirdFourthRate)}</span></div>
        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">RZ TD Rate</span><span className="contest-teamstats-stat-value">{pct(metrics.rzTdRate)}</span></div>

        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Time Poss Ratio</span><span className="contest-teamstats-stat-value">{num(metrics.timePossRatio,2)}</span></div>
        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Field Pos Diff</span><span className="contest-teamstats-stat-value">{num(metrics.fieldPosDiff,2)}</span></div>

        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Turnover Margin / Drive</span><span className="contest-teamstats-stat-value">{num(metrics.turnoverMarginPerDrive,3)}</span></div>
        <div className="contest-teamstats-item"><span className="contest-teamstats-stat-name">Penalty Yards / Play</span><span className="contest-teamstats-stat-value">{num(metrics.penaltyYardsPerPlay,2)}</span></div>
      </div>
    </div>
  );
}

export default function ContestOverviewDriveMetrics({ homeMetrics, awayMetrics }) {
  return (
    <div className="contest-section">
      <div className="contest-section-title">Drive Metrics</div>
      <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
        <div style={{ flex: '1 1 0' }}>
          <MetricsCard title="Home (Offense)" metrics={homeMetrics} />
        </div>
        <div style={{ flex: '1 1 0' }}>
          <MetricsCard title="Away (Offense)" metrics={awayMetrics} />
        </div>
      </div>
    </div>
  );
}
