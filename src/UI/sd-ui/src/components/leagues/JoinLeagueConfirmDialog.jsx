import { useEffect, useRef } from "react";
import JoinClosesLabel from "./JoinClosesLabel";
import "./JoinLeagueConfirmDialog.css";

// Shared by the discover page and the home rail so both join surfaces show
// identical details before committing. Joining is effectively irreversible
// from the user's seat (no self-serve leave), so the details render ON the
// join path, not behind a separate affordance most users would skip.

const SPORT_LABEL = {
  FootballNcaa: "NCAAFB",
  FootballNfl: "NFL",
  BaseballMlb: "MLB",
};

// PublicLeagueDto.pickType int -> the create page's abbreviations.
const PICK_TYPE_LABEL = {
  1: "SU",
  2: "ATS",
  3: "O/U",
};

// BE enum names -> user-facing phrasing (mirrors the create form's options).
const TIEBREAKER_LABEL = {
  TotalPoints: "Closest total points",
  HomeAndAwayScores: "Home and away scores",
  EarliestSubmission: "Earliest submission",
};

const formatDate = (iso) => {
  if (!iso) return null;
  const d = new Date(iso);
  return Number.isNaN(d.getTime())
    ? null
    : d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
};

const windowLabel = (league) => {
  const start = formatDate(league.startsOn);
  const end = formatDate(league.endsOn);
  if (!start && !end) return "Full Season";
  if (start && end) return `${start} – ${end}`;
  return start ? `From ${start}` : `Through ${end}`;
};

function JoinLeagueConfirmDialog({ league, onCancel, onConfirm }) {
  const dialogRef = useRef(null);

  // Keyboard modality: aria-modal alone constrains nothing. On open, move
  // focus into the dialog and remember the trigger; trap Tab inside; Escape
  // cancels; on close, hand focus back to whatever opened us.
  useEffect(() => {
    if (!league) return undefined;
    const previouslyFocused = document.activeElement;
    dialogRef.current?.focus();

    const onKeyDown = (e) => {
      if (e.key === "Escape") {
        e.preventDefault();
        onCancel();
        return;
      }
      if (e.key !== "Tab" || !dialogRef.current) return;
      const focusables = dialogRef.current.querySelectorAll(
        'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
      );
      if (focusables.length === 0) return;
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      if (previouslyFocused instanceof HTMLElement) previouslyFocused.focus();
    };
  }, [league, onCancel]);

  if (!league) return null;

  return (
    <div className="join-confirm-overlay" role="presentation" onClick={onCancel}>
      <div
        ref={dialogRef}
        tabIndex={-1}
        className="join-confirm-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="join-confirm-title"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 id="join-confirm-title">Join {league.name}?</h3>
        {league.description ? (
          <p className="join-confirm-description">{league.description}</p>
        ) : null}
        <ul>
          <li>
            <strong>Sport:</strong>{" "}
            {SPORT_LABEL[league.sport] ?? league.sport} {league.seasonYear}
          </li>
          <li>
            <strong>Pick Type:</strong> {PICK_TYPE_LABEL[league.pickType] ?? "—"}
            {league.useConfidencePoints ? " with Confidence Points" : ""}
          </li>
          <li>
            <strong>Tiebreaker:</strong>{" "}
            {TIEBREAKER_LABEL[league.tiebreakerType] ?? league.tiebreakerType}
          </li>
          <li>
            <strong>Drop Low Weeks:</strong>{" "}
            {league.dropLowWeeksCount > 0
              ? league.dropLowWeeksCount
              : "None — all weeks count"}
          </li>
          <li>
            <strong>League Window:</strong> {windowLabel(league)}
          </li>
          <li>
            <strong>Members:</strong> {league.memberCount}
          </li>
          <li>
            <strong>Commissioner:</strong> {league.commissioner}
          </li>
          <li>
            <strong>Joining:</strong>{" "}
            <JoinClosesLabel closesAtUtc={league.closesAtUtc} isJoinable={league.isJoinable} />
          </li>
        </ul>
        <div className="join-confirm-actions">
          <button type="button" className="join-confirm-cancel" onClick={onCancel}>
            Cancel
          </button>
          <button type="button" className="join-confirm-primary" onClick={onConfirm}>
            Join
          </button>
        </div>
      </div>
    </div>
  );
}

export default JoinLeagueConfirmDialog;
