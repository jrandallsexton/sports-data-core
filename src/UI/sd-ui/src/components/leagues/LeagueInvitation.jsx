import React, { useEffect, useRef, useState } from "react";
import LeaguesApi from "../../api/leagues/leaguesApi";
import "./LeagueInvitation.css";

// Success/error styling for a status message, keyed on a per-message error
// marker (e.g. "Failed", "Could not").
const messageClass = (text, errorMarker) =>
  `confirmation-message ${
    text.includes(errorMarker) ? "confirmation-error" : "confirmation-success"
  }`;

const LeagueInvitation = ({ leagueId, leagueName }) => {
  const [inviteeName, setInviteeName] = useState("");
  const [email, setEmail] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [confirmation, setConfirmation] = useState("");

  // Username/display-name autocomplete for inviting already-registered users.
  const [search, setSearch] = useState("");
  const [results, setResults] = useState([]);
  const [isSearching, setIsSearching] = useState(false);
  const [invitingUserId, setInvitingUserId] = useState(null);
  const [userInviteMessage, setUserInviteMessage] = useState("");
  const searchSeq = useRef(0);
  const mountedRef = useRef(true);

  // Track mount state so async resolutions after unmount don't write state.
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  // Debounced search: the BE requires >= 2 chars and excludes self/members.
  useEffect(() => {
    const term = search.trim();
    if (term.length < 2) {
      setResults([]);
      setIsSearching(false);
      return;
    }

    setIsSearching(true);
    const seq = ++searchSeq.current;
    const timer = setTimeout(async () => {
      try {
        const users = await LeaguesApi.searchInviteableUsers(leagueId, term);
        // Ignore out-of-order responses from earlier keystrokes, and any
        // resolution after the component unmounted.
        if (mountedRef.current && seq === searchSeq.current) setResults(users);
      } catch (err) {
        console.error("User search failed:", err);
        if (mountedRef.current && seq === searchSeq.current) setResults([]);
      } finally {
        if (mountedRef.current && seq === searchSeq.current) setIsSearching(false);
      }
    }, 300);

    return () => clearTimeout(timer);
  }, [search, leagueId]);

  const handleInviteUser = async (user) => {
    setInvitingUserId(user.userId);
    setUserInviteMessage("");
    try {
      await LeaguesApi.inviteUser(leagueId, user.userId);
      setUserInviteMessage(`Invited @${user.username}.`);
      // Drop the invited user from the list so they can't be double-invited.
      setResults((prev) => prev.filter((u) => u.userId !== user.userId));
    } catch (error) {
      console.error("Failed to invite user:", error);
      setUserInviteMessage("Could not send invite. Please try again.");
    } finally {
      setInvitingUserId(null);
    }
  };

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

      <div className="invite-search-section">
        <p>Or invite an existing member by username:</p>
        <input
          type="text"
          placeholder="Search by username or name"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="form-input"
          aria-label="Search users to invite"
        />
        {isSearching && <div className="invite-search-status">Searching…</div>}
        {!isSearching && search.trim().length >= 2 && results.length === 0 && (
          <div className="invite-search-status">No matching users.</div>
        )}
        {results.length > 0 && (
          <ul className="invite-search-results">
            {results.map((user) => (
              <li key={user.userId} className="invite-search-result">
                <span className="invite-search-identity">
                  <span className="invite-search-username">@{user.username}</span>
                  <span className="invite-search-displayname">{user.displayName}</span>
                </span>
                <button
                  type="button"
                  onClick={() => handleInviteUser(user)}
                  disabled={invitingUserId === user.userId}
                  className="invite-user-button"
                >
                  {invitingUserId === user.userId ? "Inviting…" : "Invite"}
                </button>
              </li>
            ))}
          </ul>
        )}
        {userInviteMessage && (
          <div className={messageClass(userInviteMessage, "Could not")}>
            {userInviteMessage}
          </div>
        )}
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
          <div className={messageClass(confirmation, "Failed")}>
            {confirmation}
          </div>
        )}
      </form>
    </div>
  );
};

export default LeagueInvitation;
