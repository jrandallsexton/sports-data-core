import React from "react";

const LeagueInvitation = ({ leagueId, leagueName }) => {
  // Generate dashless invite code
  const inviteCode = leagueId.replace(/-/g, "");
  const inviteUrl = `${window.location.origin}/join/${inviteCode}`;

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(inviteUrl);
      alert("Invite link copied to clipboard!");
    } catch (err) {
      console.error("Clipboard copy failed:", err);
    }
  };

  return (
    <div className="league-invitation">
      <h2>Invite Others</h2>
      <p>
        Send this link to invite others to <strong>{leagueName}</strong>:
      </p>
      <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
        <input
          type="text"
          readOnly
          value={inviteUrl}
          style={{ flex: 1, fontFamily: "monospace" }}
        />
        <button onClick={handleCopy}>Copy</button>
      </div>
    </div>
  );
};

export default LeagueInvitation;
