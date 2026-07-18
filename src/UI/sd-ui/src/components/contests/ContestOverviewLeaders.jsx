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
        {/* Player headshot removed: ESPN-sourced player headshots are licensed
            and can't ship (same constraint as team logos). NCAAFB still surfaced
            them here. Team logo + name/stat line remain. */}
        <div style={{ minWidth: 0 }}>
          <div className="contest-leader-player">{l.playerName}</div>
          {l.statLine && (
            <div className="contest-leader-statline">{l.statLine}</div>
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
              {/* Two columns: away | home. Leaders aren't sequential like the
                  play log, so side-by-side reads more intuitively than a stack. */}
              <div className="contest-leaders-columns">
                <div className="contest-leaders-column">
                  {cat.away?.leaders?.map((l, i) => (
                    <React.Fragment key={`a${i}`}>{renderPlayer(l, awayTeam)}</React.Fragment>
                  ))}
                </div>
                <div className="contest-leaders-column">
                  {cat.home?.leaders?.map((l, i) => (
                    <React.Fragment key={`h${i}`}>{renderPlayer(l, homeTeam)}</React.Fragment>
                  ))}
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
