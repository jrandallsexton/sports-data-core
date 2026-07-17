import { useState, useEffect, useRef } from "react";
import "./CloneLeagueDialog.css";

/**
 * Duplicate-league dialog: name (pre-filled "<Original> (Copy)") + an option to
 * invite the source league's members. Config and the slate are copied server-side;
 * picks are not.
 */
function CloneLeagueDialog({ league, submitting, onClose, onConfirm }) {
  const [name, setName] = useState(`${league.name} (Copy)`);
  const [inviteMembers, setInviteMembers] = useState(false);

  const dialogRef = useRef(null);

  const trimmed = name.trim();

  // Close on Escape (unless mid-submit), matching ImportPicksDialog.
  useEffect(() => {
    const onKey = (e) => {
      if (e.key === "Escape" && !submitting) onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [submitting, onClose]);

  // Restore focus to the trigger on close. Focus doesn't need moving IN on open —
  // the name input autoFocuses, which is both inside the dialog and the field the
  // user came here to edit.
  useEffect(() => {
    const previouslyFocused = document.activeElement;
    return () => {
      if (previouslyFocused instanceof HTMLElement) previouslyFocused.focus();
    };
  }, []);

  // Trap Tab / Shift+Tab within the dialog so keyboard focus can't escape to the
  // page behind the modal.
  const handleTabTrap = (e) => {
    if (e.key !== "Tab") return;
    const focusables = dialogRef.current?.querySelectorAll(
      'button:not([disabled]), select:not([disabled]), input:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'
    );
    if (!focusables || focusables.length === 0) return;
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    // The container (tabIndex -1) can hold focus if the input is disabled mid-submit;
    // treat it as before the first control so Shift+Tab wraps instead of escaping.
    const atStart =
      document.activeElement === first || document.activeElement === dialogRef.current;
    if (e.shiftKey && atStart) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      first.focus();
    }
  };

  return (
    <div className="clone-dialog-overlay" onClick={submitting ? undefined : onClose}>
      <div
        className="clone-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="clone-dialog-title"
        ref={dialogRef}
        tabIndex={-1}
        onKeyDown={handleTabTrap}
        onClick={(e) => e.stopPropagation()}
      >
        <h3 id="clone-dialog-title" className="clone-dialog-title">
          Duplicate league
        </h3>
        <p className="clone-dialog-message">
          Create a copy of <strong>{league.name}</strong> with the same settings and
          games. Picks aren&rsquo;t copied.
        </p>

        <label className="clone-dialog-field">
          <span>Name</span>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            disabled={submitting}
            autoFocus
          />
        </label>

        <label className="clone-dialog-toggle">
          <input
            type="checkbox"
            checked={inviteMembers}
            onChange={(e) => setInviteMembers(e.target.checked)}
            disabled={submitting}
          />
          Invite members from {league.name}
        </label>

        <div className="clone-dialog-buttons">
          <button
            className="clone-dialog-button cancel"
            onClick={onClose}
            disabled={submitting}
          >
            Cancel
          </button>
          <button
            className="clone-dialog-button confirm"
            onClick={() => onConfirm(trimmed, inviteMembers)}
            disabled={submitting || trimmed.length === 0}
          >
            {submitting ? "Creating…" : "Create copy"}
          </button>
        </div>
      </div>
    </div>
  );
}

export default CloneLeagueDialog;
