import { useState } from "react";
import "./CloneLeagueDialog.css";

/**
 * Duplicate-league dialog: name (pre-filled "<Original> (Copy)") + an option to
 * invite the source league's members. Config and the slate are copied server-side;
 * picks are not.
 */
function CloneLeagueDialog({ league, submitting, onClose, onConfirm }) {
  const [name, setName] = useState(`${league.name} (Copy)`);
  const [inviteMembers, setInviteMembers] = useState(false);

  const trimmed = name.trim();

  return (
    <div className="clone-dialog-overlay" onClick={submitting ? undefined : onClose}>
      <div className="clone-dialog" onClick={(e) => e.stopPropagation()}>
        <h3 className="clone-dialog-title">Duplicate league</h3>
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
