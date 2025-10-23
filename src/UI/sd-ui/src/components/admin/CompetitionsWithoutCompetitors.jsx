import React from 'react';
import './AdminPage.css';

export default function CompetitionsWithoutCompetitors({ items = [], loading, error }) {
  return (
    <section className="admin-card large">
      <h2>Competitions Without Competitors</h2>
      {loading ? (
        <div className="placeholder">Loading</div>
      ) : error ? (
        <div className="placeholder">Error: {String(error)}</div>
      ) : items.length === 0 ? (
        <div className="placeholder">No competitions found.</div>
      ) : (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'left', padding: 8 }}>CompetitionId</th>
                <th style={{ textAlign: 'left', padding: 8 }}>ContestId</th>
                <th style={{ textAlign: 'left', padding: 8 }}>StartDateUtc</th>
                <th style={{ textAlign: 'left', padding: 8 }}>CompetitionName</th>
                <th style={{ textAlign: 'right', padding: 8 }}>CompetitorCount</th>
              </tr>
            </thead>
            <tbody>
              {items.map((it) => {
                const start = it.startDateUtc;
                const isSentinel = typeof start === 'string' && start.startsWith('0001-01-01');
                let startDisplay = '-';
                if (!isSentinel) {
                  try {
                    const d = new Date(start);
                    if (!isNaN(d.getTime())) startDisplay = d.toLocaleString();
                  } catch (e) {
                    startDisplay = '-';
                  }
                }

                return (
                  <tr key={it.competitionId ?? it.contestId} style={{ borderTop: '1px solid #343a40' }}>
                    <td style={{ padding: 8 }}>{it.competitionId}</td>
                    <td style={{ padding: 8 }}>{it.contestId}</td>
                    <td style={{ padding: 8 }}>{startDisplay}</td>
                    <td style={{ padding: 8 }}>{it.competitionName ?? '-'}</td>
                    <td style={{ padding: 8, textAlign: 'right' }}>{typeof it.competitorCount === 'number' ? it.competitorCount : '-'}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
