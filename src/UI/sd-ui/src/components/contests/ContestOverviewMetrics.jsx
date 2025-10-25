import React from 'react';
import './ContestOverview.css';

function formatLabel(key) {
  // convert camelCase or PascalCase to Title Case words
  const spaced = key.replace(/([A-Z])/g, ' $1').replace(/[_-]/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

function formatValue(key, v) {
  // Keys that should be displayed raw (do not convert to percent or otherwise alter)
  const rawKeys = new Set(['penaltyyardsperplay']);
  if (v === null || v === undefined) return '-';
  // If this key must be displayed raw, return the incoming value as-is
  if (rawKeys.has(String(key).toLowerCase())) return String(v);
  const n = Number(v);
  if (Number.isNaN(n)) return String(v);
  // Treat ratios/rates between 0 and 1 as percents
  if (n > -1 && n < 1 && Math.abs(n) >= 0.001) {
    return `${(n * 100).toFixed(1)}%`;
  }
  // Small numbers: show 2 decimals
  if (Math.abs(n) < 100) return n.toFixed(2);
  // Large numbers: no decimals
  return n.toFixed(0);
}

export default function ContestOverviewMetrics({ homeMetrics = {}, awayMetrics = {}, homeName = 'Home', awayName = 'Away' }) {
  // Collect all keys
  const keys = new Set();
  Object.keys(homeMetrics || {}).forEach((k) => keys.add(k));
  Object.keys(awayMetrics || {}).forEach((k) => keys.add(k));

  // Exclude administrative keys we don't want to show
  const excluded = new Set(['competitionid', 'franchiseseasonid', 'season']);

  const sortedKeys = Array.from(keys)
    .filter((k) => !excluded.has(String(k).toLowerCase()))
    .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));

  return (
    <div className="contest-section">
  <div className="contest-section-title">Metrics</div>
      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ textAlign: 'left', padding: '8px', borderBottom: '1px solid #343a40' }}>Category</th>
              <th style={{ textAlign: 'right', padding: '8px', borderBottom: '1px solid #343a40' }}>{awayName}</th>
              <th style={{ textAlign: 'right', padding: '8px', borderBottom: '1px solid #343a40' }}>{homeName}</th>
            </tr>
          </thead>
          <tbody>
            {sortedKeys.map((k) => (
              <tr key={k}>
                <td style={{ padding: '8px 12px', color: '#b0b3b8' }}>{formatLabel(k)}</td>
                <td style={{ padding: '8px 12px', textAlign: 'right' }}>{formatValue(k, awayMetrics?.[k])}</td>
                <td style={{ padding: '8px 12px', textAlign: 'right' }}>{formatValue(k, homeMetrics?.[k])}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
