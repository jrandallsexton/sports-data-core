import React, { useEffect, useState } from "react";
import "./InsightDialog.css";
import { useUserDto } from "../../contexts/UserContext";
import { formatToUserTime } from "../../utils/timeUtils";
import { useUserTimeZone } from "../../hooks/useUserTimeZone";

/**
 * The AI matchup-preview dialog ("model predicts, LLM explains") — the
 * flagship insight surface, so it gets a real layout: a matchup header
 * built from the grid-card data that rides in with the preview (short
 * names, ranks, records, kickoff, venue, broadcasts), left-aligned prose,
 * a structured prediction panel, and the admin review controls in their
 * own clearly-bounded section.
 *
 * Responsive by breakpoint, not by lowest common denominator — the prior
 * version served the phone layout (500px, all-centered text) to every
 * screen.
 */

// "16.5 | 21.0" → ["16.5", "21.0"]; anything unparseable returns null so
// the raw string still renders rather than lying with mislabeled numbers.
function parseImpliedScore(value) {
  if (!value) return null;
  const parts = String(value)
    .split("|")
    .map((p) => p.trim())
    .filter(Boolean);
  return parts.length === 2 ? parts : null;
}

function TeamBlock({ name, shortName, logoUri, rank, wins, losses, side }) {
  const record =
    wins != null && losses != null ? `${wins}-${losses}` : null;
  return (
    <div className={`insight-team insight-team--${side}`}>
      {logoUri && (
        <img src={logoUri} alt={`${name} logo`} className="insight-team__logo" />
      )}
      <div className="insight-team__name">
        {rank ? <span className="insight-team__rank">#{rank} </span> : null}
        {name}
      </div>
      {record ? <div className="insight-team__record">{record}</div> : null}
    </div>
  );
}

function InsightDialog({
  isOpen,
  onClose,
  matchup,
  loading,
  onRejectPreview,
  onApprovePreview,
}) {
  const { userDto } = useUserDto();
  const { isAdmin } = userDto;
  const userTz = useUserTimeZone();

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

  const implied = parseImpliedScore(matchup.vegasImpliedScore);
  const awayLabel = matchup.awayShort || matchup.away;
  const homeLabel = matchup.homeShort || matchup.home;

  const metaParts = [
    matchup.startDateUtc ? formatToUserTime(matchup.startDateUtc, userTz) : null,
    matchup.venue
      ? [matchup.venue, matchup.venueCity].filter(Boolean).join(", ")
      : null,
    matchup.broadcasts || null,
  ].filter(Boolean);

  return (
    <div className="insight-dialog-overlay" onClick={onClose}>
      <div
        className="insight-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={`${matchup.away} at ${matchup.home} preview`}
        onClick={(e) => e.stopPropagation()}
      >
        <button className="close-x-button" onClick={onClose} aria-label="Close">
          &times;
        </button>

        <header className="insight-header">
          <div className="insight-header__teams">
            <TeamBlock
              side="away"
              name={matchup.away}
              shortName={awayLabel}
              logoUri={matchup.awayLogoUri}
              rank={matchup.awayRank}
              wins={matchup.awayWins}
              losses={matchup.awayLosses}
            />
            <div className="insight-header__at">@</div>
            <TeamBlock
              side="home"
              name={matchup.home}
              shortName={homeLabel}
              logoUri={matchup.homeLogoUri}
              rank={matchup.homeRank}
              wins={matchup.homeWins}
              losses={matchup.homeLosses}
            />
          </div>
          {metaParts.length > 0 && (
            <div className="insight-header__meta">{metaParts.join(" · ")}</div>
          )}
        </header>

        {loading ? (
          <div className="spinner"></div>
        ) : (
          <div className="insight-body insight-text-loaded">
            <section className="insight-section">
              <h3 className="insight-section__title">Overview</h3>
              <p className="insight-section__prose">
                {matchup.insightText || "Overview not available."}
              </p>
            </section>

            <section className="insight-section">
              <h3 className="insight-section__title">Analysis</h3>
              <p className="insight-section__prose">
                {matchup.analysis || "Analysis not available."}
              </p>
            </section>

            <section className="insight-section">
              <h3 className="insight-section__title">Vegas Implied Score</h3>
              {implied ? (
                <div className="insight-implied">
                  <span className="insight-implied__team">
                    {awayLabel} <strong>{implied[0]}</strong>
                  </span>
                  <span className="insight-implied__sep">·</span>
                  <span className="insight-implied__team">
                    {homeLabel} <strong>{implied[1]}</strong>
                  </span>
                </div>
              ) : (
                <p className="insight-section__prose">
                  {matchup.vegasImpliedScore ||
                    "Vegas implied score not available."}
                </p>
              )}
            </section>

            <section className="insight-prediction">
              <h3 className="insight-prediction__title">
                sportDeets<span className="tm-symbol">™</span> Prediction
              </h3>

              <div className="insight-prediction__stats">
                <div className="insight-stat">
                  <div className="insight-stat__label">Straight Up</div>
                  <div className="insight-stat__value">
                    {matchup.straightUpWinner || "—"}
                  </div>
                </div>
                <div className="insight-stat">
                  <div className="insight-stat__label">Against the Spread</div>
                  <div className="insight-stat__value">
                    {matchup.atsWinner || "—"}
                  </div>
                </div>
                <div className="insight-stat">
                  <div className="insight-stat__label">Projected Score</div>
                  <div className="insight-stat__value">
                    {matchup.awayScore != null && matchup.homeScore != null
                      ? `${awayLabel} ${matchup.awayScore} — ${matchup.homeScore} ${homeLabel}`
                      : "—"}
                  </div>
                </div>
              </div>

              <p className="insight-section__prose">
                {matchup.prediction || "Prediction not available."}
              </p>

              {matchup.generatedUtc && (
                <div className="insight-prediction__generated">
                  Generated {formatToUserTime(matchup.generatedUtc, userTz)}
                </div>
              )}
            </section>

            {/* Approve/reject is meaningless once the game has been played —
                isContestCompleted is server-authoritative (canonical
                STATUS_FINAL) off the preview DTO. */}
            {isAdmin && !matchup.isContestCompleted && (
              <section className="insight-admin">
                <h3 className="insight-section__title insight-admin__title">
                  Admin Review
                </h3>
                <textarea
                  className="insight-admin__note"
                  placeholder="Reason for rejection (required to reject)"
                  value={rejectionNote}
                  onChange={(e) => setRejectionNote(e.target.value)}
                  rows={3}
                />
                <div className="insight-admin__actions">
                  <button
                    onClick={() =>
                      onApprovePreview?.(matchup.contestId, matchup.id)
                    }
                    className="admin-approve-button"
                  >
                    Approve Preview
                  </button>
                  <button
                    onClick={() =>
                      onRejectPreview?.({
                        PreviewId: matchup.id,
                        ContestId: matchup.contestId,
                        RejectionNote: rejectionNote.trim(),
                      })
                    }
                    className="admin-reset-button"
                    disabled={!rejectionNote.trim()}
                  >
                    Reject Preview
                  </button>
                </div>
              </section>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default InsightDialog;
