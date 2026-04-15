import React from "react";
import "./ContestOverview.css";


export default function ContestOverviewLeaders({ homeTeam, awayTeam, leaders }) {
  const categories = leaders?.categories || [];

  const renderPlayer = (l, team) => {
    if (!l) return <div className="contest-leader-item">-</div>;
    const logoUrl = team?.logoUrl;
    return (
      <div className="contest-leader-item" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
        {logoUrl && (
          <img
            src={logoUrl}
            alt={team?.displayName || ''}
            className="contest-leader-team-logo"
          />
        )}
        {l.playerHeadshotUrl && (
          <img
            src={l.playerHeadshotUrl}
            alt={l.playerName}
            style={{ width: '36px', height: '36px', borderRadius: '50%', objectFit: 'cover', flexShrink: 0 }}
          />
        )}
        <div style={{ minWidth: 0 }}>
          <span className="contest-leader-player">{l.playerName}</span>
          {l.statLine && (
            <span className="contest-leader-statline"> - {l.statLine}</span>
          )}
        </div>
      </div>
    );
  };

  return (
    <div className="contest-section">
      <div className="contest-section-title">Leaders</div>
      <div className="contest-leaders-section">
        {categories.length === 0 ? (
          <div className="contest-leader-item">No leaders available.</div>
        ) : (
          categories.map((cat, idx) => (
            <div
              key={cat.categoryId || idx}
              className="contest-leaders-panel"
              style={{
                background: "var(--bg-input)",
                border: "1px solid var(--border-primary)",
                borderRadius: 10,
                boxShadow: "0 1px 6px rgba(33,150,243,0.07)",
                marginBottom: 12,
                padding: "10px 14px"
              }}
            >
              <div className="contest-leader-category-header">
                {cat.categoryName}
              </div>
              <div className="contest-leaders-stacked">
                {cat.away?.leaders?.length > 0 &&
                  cat.away.leaders.map((l, i) => <React.Fragment key={`a${i}`}>{renderPlayer(l, awayTeam)}</React.Fragment>)}
                {cat.home?.leaders?.length > 0 &&
                  cat.home.leaders.map((l, i) => <React.Fragment key={`h${i}`}>{renderPlayer(l, homeTeam)}</React.Fragment>)}
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
