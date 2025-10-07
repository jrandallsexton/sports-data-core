import React from "react";
import "./ContestOverview.css";


export default function ContestOverviewLeaders({ homeTeam, awayTeam, leaders }) {
  const categories = leaders?.categories || [];
  return (
    <div className="contest-section">
      <div className="contest-section-title">Leaders</div>
      <div className="contest-leaders-section">
        {categories.length === 0 ? (
          <div className="contest-leader-item">No leaders available.</div>
        ) : (
          categories.map((cat, idx) => (
            <div key={cat.categoryId || idx} className="contest-leaders-row" style={{ alignItems: 'center', marginBottom: 12 }}>
              {/* Away leaders */}
              <div className="contest-leaders-team contest-leaders-away">
                {cat.away?.leaders && cat.away.leaders.length > 0 ? (
                  cat.away.leaders.map((l, i) => (
                    <div key={i} className="contest-leader-item">
                      <span className="contest-leader-player">{l.playerName}</span>
                      <div className="contest-leader-statline">{l.statLine}</div>
                    </div>
                  ))
                ) : (
                  <div className="contest-leader-item">-</div>
                )}
              </div>
              {/* Category name center */}
              <div className="contest-leader-category" style={{ minWidth: 120, textAlign: 'center', fontWeight: 600 }}>
                {cat.categoryName}
              </div>
              {/* Home leaders */}
              <div className="contest-leaders-team contest-leaders-home">
                {cat.home?.leaders && cat.home.leaders.length > 0 ? (
                  cat.home.leaders.map((l, i) => (
                    <div key={i} className="contest-leader-item">
                      <span className="contest-leader-player">{l.playerName}</span>
                      <div className="contest-leader-statline">{l.statLine}</div>
                    </div>
                  ))
                ) : (
                  <div className="contest-leader-item">-</div>
                )}
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
