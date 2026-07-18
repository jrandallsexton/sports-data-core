import React, { useState } from "react";
import toast from "react-hot-toast";
import apiWrapper from "../../api/apiWrapper";

/**
 * Admin-only operations on a single contest. Visibility is the parent's
 * responsibility — ContestOverview already gates on userDto.isAdmin and
 * skips rendering this component when the user isn't admin.
 *
 * The buttons here are deliberately low-style: each one fires a request
 * that Producer turns into background work (refresh sourcing, refresh
 * media, force finalize). None of them block the user, so the UX is
 * "click and watch the toast / Seq."
 *
 * Lives in the .contest-section visual family so it slots cleanly into
 * the second column of the overview grid right after Metrics.
 */
export default function ContestOverviewAdmin({ contestId, sport, league }) {
  const [refreshing, setRefreshing] = useState(false);
  const [refreshError, setRefreshError] = useState(null);
  const [refreshingMedia, setRefreshingMedia] = useState(false);
  const [refreshMediaError, setRefreshMediaError] = useState(null);
  const [finalizing, setFinalizing] = useState(false);
  const [finalizeError, setFinalizeError] = useState(null);
  // Re-enrichment state. correlationId is what Producer logged the work
  // under and is what we surface in the UI so the operator can paste it
  // into Seq for tracing.
  const [reenriching, setReenriching] = useState(false);
  const [reenrichError, setReenrichError] = useState(null);
  const [reenrichCorrelationId, setReenrichCorrelationId] = useState(null);

  const handleRefresh = async () => {
    setRefreshing(true);
    setRefreshError(null);
    try {
      await apiWrapper.Contest.refresh(contestId, sport, league);
      toast.success("Contest refresh request submitted.");
    } catch (err) {
      setRefreshError("Failed to refresh contest.");
      toast.error("Failed to submit refresh request.");
    }
    setRefreshing(false);
  };

  const handleRefreshMedia = async () => {
    setRefreshingMedia(true);
    setRefreshMediaError(null);
    try {
      await apiWrapper.Contest.refreshMedia(contestId, sport, league);
      toast.success("Media refresh request submitted.");
    } catch (err) {
      setRefreshMediaError("Failed to refresh media.");
      toast.error("Failed to submit media refresh request.");
    }
    setRefreshingMedia(false);
  };

  const handleFinalize = async () => {
    setFinalizing(true);
    setFinalizeError(null);
    try {
      await apiWrapper.Contest.finalize(contestId, sport, league);
      toast.success("Finalize contest request submitted.");
    } catch (err) {
      setFinalizeError("Failed to finalize contest.");
      toast.error("Failed to submit finalize request.");
    }
    setFinalizing(false);
  };

  const handleReenrich = async () => {
    setReenriching(true);
    setReenrichError(null);
    // Clear the prior correlationId display so a failure doesn't leave
    // the stale-from-a-previous-success id sitting on screen looking
    // like it belongs to the new attempt.
    setReenrichCorrelationId(null);
    try {
      const response = await apiWrapper.Admin.reenrichContest(contestId, sport, league);
      // API handler returns Result<Guid> where the Guid IS the
      // CorrelationId Producer logged the work under (see
      // ReenrichContestCommandHandler: Success<Guid>(producerCorrelationId)).
      const correlationId = response?.data ?? null;
      setReenrichCorrelationId(correlationId);
      toast.success("Re-enrichment complete.");
    } catch (err) {
      setReenrichError("Failed to re-run enrichment.");
      toast.error("Failed to re-run enrichment.");
    }
    setReenriching(false);
  };

  // Shared button style — keeps each action visually equivalent and
  // makes adding the next admin action a one-line copy. Inline because
  // there's no Admin-specific class set in ContestOverview.css yet;
  // promote to a class if this list grows.
  //
  // Color note: prior version used a hardcoded #23272f background, but
  // that matches --bg-input exactly in the dark theme — buttons rendered
  // invisible against the .contest-section background. Use --accent for
  // a high-contrast call-to-action treatment that works in both themes.
  const buttonStyle = {
    width: "100%",
    padding: "10px 24px",
    fontSize: 16,
    fontWeight: 600,
    borderRadius: 6,
    background: "var(--accent)",
    color: "var(--text-on-accent)",
    border: "none",
    cursor: "pointer",
  };

  return (
    <div className="contest-section">
      <div className="contest-section-title">Admin</div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, alignItems: "start" }}>
        <div>
          <button onClick={handleRefresh} disabled={refreshing} style={buttonStyle}>
            {refreshing ? "Refreshing..." : "Refresh Contest"}
          </button>
          {refreshError && (
            <div style={{ color: "#d32f2f", marginTop: 6 }}>{refreshError}</div>
          )}
        </div>

        <div>
          <button onClick={handleRefreshMedia} disabled={refreshingMedia} style={buttonStyle}>
            {refreshingMedia ? "Refreshing..." : "Refresh Media"}
          </button>
          {refreshMediaError && (
            <div style={{ color: "#d32f2f", marginTop: 6 }}>{refreshMediaError}</div>
          )}
        </div>

        <div>
          <button onClick={handleFinalize} disabled={finalizing} style={buttonStyle}>
            {finalizing ? "Finalizing..." : "Finalize Contest"}
          </button>
          {finalizeError && (
            <div style={{ color: "#d32f2f", marginTop: 6 }}>{finalizeError}</div>
          )}
        </div>

        <div>
          <button onClick={handleReenrich} disabled={reenriching} style={buttonStyle}>
            {reenriching ? "Re-running enrichment..." : "Re-run Enrichment"}
          </button>
          {reenrichError && (
            <div style={{ color: "#d32f2f", marginTop: 6 }}>{reenrichError}</div>
          )}
          {reenrichCorrelationId && (
            // monospace + userSelect:'all' so a click highlights the
            // whole id for paste into Seq without selection drag.
            <div style={{ marginTop: 6, fontSize: 14, color: "var(--text-secondary)" }}>
              CorrelationId:{" "}
              <code
                style={{
                  fontFamily: "monospace",
                  userSelect: "all",
                  background: "var(--bg-card, rgba(255,255,255,0.04))",
                  padding: "2px 6px",
                  borderRadius: 4,
                  color: "var(--text-primary)",
                }}
              >
                {reenrichCorrelationId}
              </code>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
