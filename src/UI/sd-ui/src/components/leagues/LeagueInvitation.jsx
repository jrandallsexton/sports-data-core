import React, { useState } from "react";
import LeaguesApi from "../../api/leagues/leaguesApi";
import "./LeagueInvitation.css";

const LeagueInvitation = ({ leagueId, leagueName }) => {
  const [inviteeName, setInviteeName] = useState("");
  const [email, setEmail] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [confirmation, setConfirmation] = useState("");

  // Generate dashless invite code
  const inviteCode = leagueId.replace(/-/g, "");
  const inviteUrl = `${window.location.origin}/app/join/${inviteCode}`;

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(inviteUrl);
      alert("Invite link copied to clipboard!");
    } catch (err) {
      console.error("Clipboard copy failed:", err);
    }
  };

  const handleSendInvite = async (e) => {
    e.preventDefault();
    setIsSending(true);
    setConfirmation("");

    try {
      await LeaguesApi.sendInvite(leagueId, email, inviteeName);
      setConfirmation("Invitation sent!");
      setInviteeName("");
      setEmail("");
    } catch (error) {
      console.error("Failed to send invite:", error);
      setConfirmation("Failed to send invitation. Please try again.");
    } finally {
      setIsSending(false);
    }
  };

  return (
    <div className="league-invitation">
      <h2>Invite Others</h2>

      <p>
        Share this link to invite others to <strong>{leagueName}</strong>:
      </p>
      
      <div className="invite-link-section">
        <input
          type="text"
          readOnly
          value={inviteUrl}
          className="invite-link-input"
        />
        <button onClick={handleCopy} className="copy-button">
          Copy
        </button>
      </div>

      <form onSubmit={handleSendInvite} className="invite-form">
        <div className="form-group">
          <input
            type="text"
            placeholder="Recipient name (optional)"
            value={inviteeName}
            onChange={(e) => setInviteeName(e.target.value)}
            className="form-input"
          />
        </div>
        
        <div className="form-group">
          <input
            type="email"
            placeholder="Recipient email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            className="form-input"
          />
        </div>
        
        <button type="submit" disabled={isSending} className="send-button">
          {isSending ? "Sending..." : "Send Invitation"}
        </button>
        
        {confirmation && (
          <div
            className={`confirmation-message ${
              confirmation.includes("Failed") ? "confirmation-error" : "confirmation-success"
            }`}
          >
            {confirmation}
          </div>
        )}
      </form>
    </div>
  );
};

export default LeagueInvitation;
