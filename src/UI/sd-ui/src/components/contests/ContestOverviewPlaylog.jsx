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
        <div className="contest-scoring-summary-list">
          {filteredPlays && filteredPlays.length > 0 ? (
            filteredPlays.map((play, idx) => {
              const logoUrl = getLogoUrl(play.team);
              return (
                <div key={idx} className="contest-scoring-summary-item">
                  <span className="contest-scoring-summary-quarter">Q{play.quarter}</span>
                  {logoUrl && (
                    <img
                      src={logoUrl}
                      alt={play.team}
                      className="contest-scoring-summary-logo"
                      style={{ width: 28, height: 28, objectFit: 'contain', marginRight: 8, verticalAlign: 'middle' }}
                    />
                  )}
                  {/* Removed team slug text from UI, only show logo */}
                  <span className="contest-scoring-summary-desc">{play.description}</span>
                  <span className="contest-scoring-summary-time">{play.timeRemaining}</span>
                </div>
              );
            })
          ) : (
            <div className="contest-scoring-summary-item">No play log available.</div>
          )}
        </div>
      </div>
    </div>
  );
}
