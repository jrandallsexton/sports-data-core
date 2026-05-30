import React, { useEffect, useState } from "react";
import { getPeriodPrefix } from "../../utils/periodLabel";
import apiWrapper from "../../api/apiWrapper";
import "./ContestOverview.css";

/**
 * Renders the "Play Log" panel on the Contest Overview page.
 *
 * The `playLog` prop carries only the *significant* plays (scoring or
 * marked-priority) that the backend's overview endpoint returns by default
 * to keep the payload small (~10 rows vs. 500+ for MLB). When the user
 * checks "Show all plays" we fetch the full log from `/ui/contest/:id/playlog`
 * on demand and cache it in component state so subsequent toggles don't
 * re-fetch.
 */
export default function ContestOverviewPlaylog({ playLog, sport, contestId, league }) {
  const periodPrefix = getPeriodPrefix(sport);
  const [showAll, setShowAll] = useState(false);
  // Lazy-loaded full play log. Null until the user first opts in.
  const [fullPlayLog, setFullPlayLog] = useState(null);
  const [loadingFull, setLoadingFull] = useState(false);
  const [fullError, setFullError] = useState(null);

  // Reset lazy-load state when the contest context changes. The parent
  // page (ContestOverview) is rendered by a React Router route that
  // matches the same definition for every contest id, so navigating
  // between contests re-renders this component instance with new props
  // rather than remounting it — without this effect the cached
  // fullPlayLog from a prior contest would leak into the next one.
  useEffect(() => {
    setShowAll(false);
    setFullPlayLog(null);
    setLoadingFull(false);
    setFullError(null);
  }, [contestId, sport, league]);

  if (!playLog || !playLog.plays) return null;

  const handleToggle = () => {
    const next = !showAll;
    setShowAll(next);

    // First time turning on → fetch. Cached on subsequent toggles so
    // bouncing the checkbox doesn't re-hit the API.
    if (next && !fullPlayLog && !loadingFull) {
      setLoadingFull(true);
      setFullError(null);
      apiWrapper.Contest.getContestPlayLog(contestId, sport, league)
        .then((result) => {
          const dto = result?.data || result;
          setFullPlayLog(dto);
          setLoadingFull(false);
        })
        .catch((err) => {
          setFullError(err);
          setLoadingFull(false);
        });
    }
  };

  // While the full-log fetch is in flight, keep showing the
  // significant-plays subset so the user has continuous context. Swap to
  // the full log once it arrives.
  const activePlayLog = showAll && fullPlayLog ? fullPlayLog : playLog;
  const { plays, awayTeamSlug, homeTeamSlug, awayTeamLogoUrl, homeTeamLogoUrl } = activePlayLog;

  // Helper to get logo URL for a play
  const getLogoUrl = (teamSlug) => {
    if (teamSlug === awayTeamSlug) return awayTeamLogoUrl;
    if (teamSlug === homeTeamSlug) return homeTeamLogoUrl;
    return null;
  };

  // Group plays by quarter
  const playsByQuarter = plays.reduce((acc, play) => {
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
        <label style={{ fontSize: 14, fontWeight: 400, cursor: loadingFull ? 'wait' : 'pointer', userSelect: 'none' }}>
          <input
            type="checkbox"
            checked={showAll}
            onChange={handleToggle}
            disabled={loadingFull}
            style={{ marginRight: 6 }}
          />
          {loadingFull ? 'Loading all plays…' : 'Show all plays'}
        </label>
      </div>
      <div className="contest-scoring-summary-section">
        {fullError && showAll && (
          <div className="contest-scoring-summary-item" style={{ color: 'var(--danger, #d32f2f)', marginBottom: 8 }}>
            Failed to load full play log. Showing significant plays only.
          </div>
        )}
        {quarterOrder.length > 0 ? (
          quarterOrder.map((quarter) => (
            <div
              key={quarter}
              className="contest-playlog-panel"
              style={{
                background: "var(--bg-input)",
                border: "1px solid var(--border-primary)",
                borderRadius: 10,
                boxShadow: "0 1px 6px rgba(33,150,243,0.07)",
                marginBottom: 16,
                padding: "14px 18px"
              }}
            >
              <div style={{ fontWeight: 700, color: 'var(--warning)', marginBottom: 8 }}>{periodPrefix}{quarter}</div>
              <div className="contest-scoring-summary-list">
                {playsByQuarter[quarter].map((play, idx) => {
                  const logoUrl = getLogoUrl(play.team);
                  return (
                    <div key={idx} className="contest-scoring-summary-item">
                      {logoUrl && (
                        <div className="contest-scoring-summary-logo-wrap">
                          <img
                            src={logoUrl}
                            alt={play.team}
                            className="contest-scoring-summary-logo"
                            style={{ width: 20, height: 20, objectFit: 'contain', verticalAlign: 'middle' }}
                          />
                        </div>
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
