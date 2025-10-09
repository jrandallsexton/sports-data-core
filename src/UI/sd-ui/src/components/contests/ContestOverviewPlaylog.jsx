import React, { useState } from "react";
import "./ContestOverview.css";

export default function ContestOverviewPlaylog({ playLog }) {
  const [showAll, setShowAll] = useState(false);
  if (!playLog || !playLog.plays) return null;
  const { plays, awayTeamSlug, homeTeamSlug, awayTeamLogoUrl, homeTeamLogoUrl } = playLog;
  const filteredPlays = !showAll ? plays.filter(p => p.isScoringPlay) : plays;

  // Helper to get logo URL for a play
  const getLogoUrl = (teamSlug) => {
    if (teamSlug === awayTeamSlug) return awayTeamLogoUrl;
    if (teamSlug === homeTeamSlug) return homeTeamLogoUrl;
    return null;
  };


  // Group plays by quarter
  const playsByQuarter = filteredPlays.reduce((acc, play) => {
    const q = play.quarter || "Other";
    if (!acc[q]) acc[q] = [];
    acc[q].push(play);
    return acc;
  }, {});

  const quarterOrder = Object.keys(playsByQuarter).sort((a, b) => Number(a) - Number(b));

  return (
    <div className="contest-section">
      <div className="contest-section-title" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span>Play Log</span>
        <label style={{ fontSize: 14, fontWeight: 400, cursor: 'pointer', userSelect: 'none' }}>
          <input
            type="checkbox"
            checked={showAll}
            onChange={() => setShowAll(v => !v)}
            style={{ marginRight: 6 }}
          />
          Show all plays
        </label>
      </div>
      <div className="contest-scoring-summary-section">
        {quarterOrder.length > 0 ? (
          quarterOrder.map((quarter) => (
            <div
              key={quarter}
              className="contest-playlog-panel"
              style={{
                background: "#23272f",
                border: "1px solid #343a40",
                borderRadius: 10,
                boxShadow: "0 1px 6px rgba(33,150,243,0.07)",
                marginBottom: 16,
                padding: "14px 18px"
              }}
            >
              <div style={{ fontWeight: 700, color: '#ffc107', marginBottom: 8 }}>Q{quarter}</div>
              <div className="contest-scoring-summary-list">
                {playsByQuarter[quarter].map((play, idx) => {
                  const logoUrl = getLogoUrl(play.team);
                  return (
                    <div key={idx} className="contest-scoring-summary-item">
                      {logoUrl && (
                        <img
                          src={logoUrl}
                          alt={play.team}
                          className="contest-scoring-summary-logo"
                          style={{ width: 28, height: 28, objectFit: 'contain', marginRight: 8, verticalAlign: 'middle' }}
                        />
                      )}
                      <span className="contest-scoring-summary-desc">{play.description}</span>
                      <span className="contest-scoring-summary-time">{play.timeRemaining}</span>
                    </div>
                  );
                })}
              </div>
            </div>
          ))
        ) : (
          <div className="contest-scoring-summary-item">No play log available.</div>
        )}
      </div>
    </div>
  );
}
