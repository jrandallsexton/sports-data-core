import React, { useEffect, useState } from "react";
import "./InsightDialog.css";
import { useUserDto } from "../../contexts/UserContext";

function InsightDialog({
  isOpen,
  onClose,
  matchup,
  loading,
  onRejectPreview,
}) {

  const { userDto } = useUserDto();
  const { isAdmin } = userDto;

  // Local state for rejection note
  const [rejectionNote, setRejectionNote] = useState("");

  useEffect(() => {
    if (isOpen) {
      document.body.classList.add("modal-open");
    } else {
      document.body.classList.remove("modal-open");
    }

    return () => {
      document.body.classList.remove("modal-open");
    };
  }, [isOpen]);

  if (!isOpen || !matchup) return null;

  return (
    <div className="insight-dialog-overlay" onClick={onClose}>
      <div className="insight-dialog" onClick={(e) => e.stopPropagation()}>
        <button className="close-x-button" onClick={onClose}>
          &times;
        </button>

        <div className="helmet-row">
          {matchup.awayLogoUri && (
            <img
              src={matchup.awayLogoUri}
              alt={`${matchup.away} logo`}
              className="matchup-logo"
            />
          )}
          <h2>
            {matchup.away}
            <br />@<br />
            {matchup.home}
          </h2>
          {matchup.homeLogoUri && (
            <img
              src={matchup.homeLogoUri}
              alt={`${matchup.home} logo`}
              className="matchup-logo"
            />
          )}
        </div>
        <hr className="divider" />

        <div className="insight-text">
          {loading ? (
            <div className="spinner"></div>
          ) : (
            <div className="insight-text-loaded">
              <div className="overview-section">
                <h3>Overview</h3>
                <p>{matchup.insightText || "Overview not available."}</p>
              </div>

              <hr className="divider" />

              <div className="analysis-section">
                <h3>Analysis</h3>
                <p>{matchup.analysis || "Analysis not available."}</p>
              </div>

              <hr className="divider" />

              <div className="vegas-section">
                <h3>Vegas Implied</h3>
                <p>
                  {matchup.vegasImpliedScore ||
                    "Vegas implied score not available."}
                </p>
              </div>

              <hr className="divider" />

              <div className="prediction-section">
                <h3>
                  sportDeets<span className="tm-symbol">™</span> Prediction
                </h3>
                <p>{matchup.prediction || "Prediction not available."}</p>

                {/* Additional prediction details */}
                <div className="prediction-details">
                  <p>
                    Straight Up Winner:{" "}
                    {matchup.straightUpWinner || "Not available"}
                  </p>
                  <p>ATS Winner: {matchup.atsWinner || "Not available"}</p>
                  <p>
                    Score: {matchup.awayScore || "N/A"} -{" "}
                    {matchup.homeScore || "N/A"}
                  </p>
                </div>
              </div>
            </div>
          )}
        </div>

        {isAdmin && !loading && (
          <div className="admin-controls">
            <textarea
              className="admin-rejection-note"
              placeholder="Enter reason for rejection..."
              value={rejectionNote}
              onChange={e => setRejectionNote(e.target.value)}
              rows={3}
              style={{ width: '100%', maxWidth: '500px', marginBottom: '0.5rem', boxSizing: 'border-box' }}
            />
            <button
              onClick={() => onRejectPreview?.({
                PreviewId: matchup.id,
                ContestId: matchup.contestId,
                RejectionNote: rejectionNote.trim()
              })}
              className="admin-reset-button"
              disabled={!rejectionNote.trim()}
            >
              Reject Preview
            </button>
          </div>
        )}

        <button className="close-button" onClick={onClose}>
          Close
        </button>
      </div>
    </div>
  );
}

export default InsightDialog;
