import React from "react";
import "./ContestOverview.css";


export default function ContestOverviewLeaders({ homeTeam, awayTeam, leaders }) {
  const categories = leaders?.categories || [];
  // Helper to decide if statLine is 'simple' (inline) or 'complex' (break line)
  const isSimpleStat = (statLine) => {
    if (!statLine) return true;
    // Consider simple if it's a number or short string without comma/space
    if (typeof statLine === 'number') return true;
    if (statLine.length <= 5 && !statLine.match(/[ ,]/)) return true;
    // If statLine contains comma, multiple stats, or is long, treat as complex
    if (statLine.length > 12 || statLine.includes(',') || statLine.match(/\d+\s+\w+/)) return false;
    return true;
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
                background: "#23272f",
                border: "1px solid #343a40",
                borderRadius: 10,
                boxShadow: "0 1px 6px rgba(33,150,243,0.07)",
                marginBottom: 16,
                padding: "14px 18px"
              }}
            >
              <div className="contest-leaders-row">
                {/* Away leaders */}
                <div className="contest-leaders-team contest-leaders-away">
                  {cat.away?.leaders && cat.away.leaders.length > 0 ? (
                    cat.away.leaders.map((l, i) => (
                      <div key={i} className="contest-leader-item">
                        {isSimpleStat(l.statLine) ? (
                          <span>
                            <span className="contest-leader-player">{l.playerName}</span>
                            {" - "}
                            <span className="contest-leader-statline">{l.statLine}</span>
                          </span>
                        ) : (
                          <>
                            <span className="contest-leader-player">{l.playerName}</span>
                            <div className="contest-leader-statline">{l.statLine}</div>
                          </>
                        )}
                      </div>
                    ))
                  ) : (
                    <div className="contest-leader-item">-</div>
                  )}
                </div>
                {/* Category name center */}
                <div className="contest-leader-category" style={{ minWidth: 120, textAlign: 'center', fontWeight: 600, color: '#b0b3b8' }}>
                  {cat.categoryName}
                </div>
                {/* Home leaders */}
                <div className="contest-leaders-team contest-leaders-home">
                  {cat.home?.leaders && cat.home.leaders.length > 0 ? (
                    cat.home.leaders.map((l, i) => (
                      <div key={i} className="contest-leader-item">
                        {isSimpleStat(l.statLine) ? (
                          <span>
                            <span className="contest-leader-player">{l.playerName}</span>
                            {" - "}
                            <span className="contest-leader-statline">{l.statLine}</span>
                          </span>
                        ) : (
                          <>
                            <span className="contest-leader-player">{l.playerName}</span>
                            <div className="contest-leader-statline">{l.statLine}</div>
                          </>
                        )}
                      </div>
                    ))
                  ) : (
                    <div className="contest-leader-item">-</div>
                  )}
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
