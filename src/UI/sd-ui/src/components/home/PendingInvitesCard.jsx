import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import LeaguesApi from "api/leagues/leaguesApi";
import { useUserDto } from "../../contexts/UserContext";
import JoinClosesLabel from "../leagues/JoinClosesLabel";
import JoinLeagueConfirmDialog from "../leagues/JoinLeagueConfirmDialog";
import { leaguePicksPath } from "../../routes/paths";
import "./PendingInvitesCard.css";

const SPORT_LABEL = {
  FootballNcaa: "NCAAFB",
  FootballNfl: "NFL",
  BaseballMlb: "MLB",
};

// Same glyph convention as the other home cards.
const SPORT_ICON = {
  FootballNcaa: "🏈",
  FootballNfl: "🏈",
  BaseballMlb: "⚾",
};

/**
 * "Pending Invitations" — league invites awaiting the user's answer. Closes
 * the gap where a push notification (and its launcher badge) implied
 * something was waiting in the app but no in-app surface showed it.
 *
 * Accept opens the SAME JoinLeagueConfirmDialog used by public-league
 * discovery (each invitation embeds the league's full parameters), so every
 * join surface shows identical details before committing. Decline is inline.
 * Renders nothing while loading or when there are no pending invites.
 */
function PendingInvitesCard() {
  const navigate = useNavigate();
  const { refreshUserDto } = useUserDto();
  const [invites, setInvites] = useState(null);
  // The invitation whose league-parameters dialog is open.
  const [confirming, setConfirming] = useState(null);
  // invitationId -> "accept" | "decline" while in flight, or "error".
  const [busy, setBusy] = useState({});

  useEffect(() => {
    let cancelled = false;
    LeaguesApi.getPendingInvitations()
      .then((data) => {
        if (!cancelled) setInvites(data || []);
      })
      .catch((err) => {
        console.error("Failed to load pending invitations:", err);
        if (!cancelled) setInvites([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  if (!invites || invites.length === 0) return null;

  const removeRow = (invitationId) =>
    setInvites((rows) => rows.filter((r) => r.invitationId !== invitationId));

  const accept = async (invite) => {
    setConfirming(null);
    setBusy((b) => ({ ...b, [invite.invitationId]: "accept" }));
    try {
      const leagueId = await LeaguesApi.acceptInvitation(invite.invitationId);
      // PicksPage's league dropdown is driven by the cached /user/me DTO —
      // refresh it BEFORE navigating or the new league isn't in the list and
      // the page falls back to its default league. Same pattern as
      // LeagueCreatePage post-create. On refresh failure the join DID
      // succeed — keep the row with a retryable error instead of navigating
      // with a stale DTO; re-accepting is idempotent server-side, so the
      // retry re-runs accept + refresh cleanly.
      const refreshed = await refreshUserDto();
      if (!refreshed) {
        setBusy((b) => ({ ...b, [invite.invitationId]: "error" }));
        return;
      }
      removeRow(invite.invitationId);
      // Land on the joined league's picks; LeaguePicksRouter renders
      // whichever game the league plays.
      navigate(leaguePicksPath(leagueId));
    } catch (err) {
      console.error("Failed to accept invitation:", err);
      setBusy((b) => ({ ...b, [invite.invitationId]: "error" }));
    }
  };

  const decline = async (invite) => {
    setBusy((b) => ({ ...b, [invite.invitationId]: "decline" }));
    try {
      await LeaguesApi.declineInvitation(invite.invitationId);
      removeRow(invite.invitationId);
    } catch (err) {
      console.error("Failed to decline invitation:", err);
      setBusy((b) => ({ ...b, [invite.invitationId]: "error" }));
    }
  };

  return (
    <section className="pending-invites-card">
      <div className="pending-invites-header">
        <h3>Pending Invitations</h3>
      </div>
      <ul className="pending-invites-list">
        {invites.map((invite) => {
          const state = busy[invite.invitationId];
          const inFlight = state === "accept" || state === "decline";
          const league = invite.league;
          return (
            <li key={invite.invitationId} className="pending-invite-row">
              <div className="pending-invite-info">
                <span className="pending-invite-name">{league.name}</span>
                <span className="pending-invite-meta">
                  <span aria-hidden="true">{SPORT_ICON[league.sport] ?? "🏆"}</span>{" "}
                  {SPORT_LABEL[league.sport] ?? league.sport} {league.seasonYear} ·
                  Invited by {invite.invitedBy}
                  {" · "}
                  <JoinClosesLabel
                    closesAtUtc={league.closesAtUtc}
                    isJoinable={league.isJoinable}
                    verb="Expires"
                  />
                  {state === "error" && (
                    <span className="pending-invite-error"> · Failed — try again</span>
                  )}
                </span>
              </div>
              <div className="pending-invite-actions">
                <button
                  type="button"
                  className="pending-invite-decline"
                  disabled={inFlight}
                  onClick={() => decline(invite)}
                >
                  {state === "decline" ? "…" : "Decline"}
                </button>
                <button
                  type="button"
                  className="pending-invite-accept"
                  disabled={inFlight}
                  onClick={() => setConfirming(invite)}
                >
                  {state === "accept" ? "Joining…" : "Accept"}
                </button>
              </div>
            </li>
          );
        })}
      </ul>
      {/* Same dialog as discovery — league parameters shown before joining. */}
      <JoinLeagueConfirmDialog
        league={confirming?.league ?? null}
        closesVerb="Expires"
        onCancel={() => setConfirming(null)}
        onConfirm={() => confirming && accept(confirming)}
      />
    </section>
  );
}

export default PendingInvitesCard;
