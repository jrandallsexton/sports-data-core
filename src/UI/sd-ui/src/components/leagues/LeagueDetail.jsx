import React, { useEffect, useState } from "react";
import { useUserDto } from "../../contexts/UserContext";
import { useParams, useNavigate, Link } from "react-router-dom";
import leaguesApi from "../../api/leagues/leaguesApi";
import "./LeagueDetail.css";
import LeagueInvitation from "./LeagueInvitation";

// Render the league window as a human-readable date range, or "Full Season"
// when both bounds are null (the league was created without a custom window).
//
// Date-only UTC-midnight strings (e.g. "2026-04-19T00:00:00Z") describe a
// calendar day, not an instant — constructing new Date() against them would
// shift the displayed date to the previous day for users west of UTC. For
// that shape we build a Date from local components so the label reflects
// the intended calendar day regardless of viewer timezone. All other ISO
// forms (local-midnight-as-UTC produced by the league-create form, real
// end-of-day timestamps, etc.) fall through to the standard UTC-aware path.
//
// Module-scoped so the regex and function aren't rebuilt on every render.
const DATE_ONLY_UTC_MIDNIGHT = /^(\d{4})-(\d{2})-(\d{2})T00:00:00(?:\.0+)?Z$/;
const formatWindowBound = (iso) => {
  if (!iso) return null;
  const dateOnlyMatch = DATE_ONLY_UTC_MIDNIGHT.exec(iso);
  const d = dateOnlyMatch
    ? new Date(Number(dateOnlyMatch[1]), Number(dateOnlyMatch[2]) - 1, Number(dateOnlyMatch[3]))
    : new Date(iso);
  return Number.isNaN(d.getTime())
    ? null
    : d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
};

const LeagueDetail = () => {
  const { userDto, loading: userLoading } = useUserDto();
  const { id } = useParams();
  const navigate = useNavigate();
  const [league, setLeague] = useState(null);
  const [loading, setLoading] = useState(true);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    const fetchLeague = async () => {
      try {
        const data = await leaguesApi.getLeagueById(id);
        setLeague(data);
      } catch (err) {
        console.error("Failed to fetch league:", err);
        setLeague(null);
      } finally {
        setLoading(false);
      }
    };

    fetchLeague();
  }, [id]);

  const handleDelete = async () => {
    setDeleting(true);
    try {
      await leaguesApi.deleteLeague(id);
      navigate("/app/league");
    } catch (err) {
      console.error("Failed to delete league:", err);
      // Surface the specific server error (e.g. "Cannot delete a league that
      // already has user picks.") instead of a generic string so the user
      // knows why the delete was refused.
      const serverMessage = err?.response?.data?.errors?.[0]?.errorMessage;
      alert(serverMessage || "Error deleting league. Please try again.");
    } finally {
      setDeleting(false);
    }
  };

  if (loading || userLoading) return <p>Loading league details...</p>;
  if (!league) return <p>League not found.</p>;

  const commissioner = league.members.find((m) => m.role === "commissioner");
  const commissionerId = commissioner?.userId;
  const isCommissioner = userDto?.id === commissionerId;

  const startLabel = formatWindowBound(league.startsOn);
  const endLabel = formatWindowBound(league.endsOn);
  let windowLabel;
  if (!startLabel && !endLabel) windowLabel = "Full Season";
  else if (startLabel && endLabel) windowLabel = `${startLabel} – ${endLabel}`;
  else if (startLabel) windowLabel = `From ${startLabel}`;
  else windowLabel = `Through ${endLabel}`;

  return (
    <div className="league-detail-container">
      <Link to="/app/league" className="back-to-leagues">
        ← My Leagues
      </Link>

      <div className="league-detail-primary">
        <div className="league-info-card">
          <h2>{league.name}</h2>
          <ul className="league-details-list">
            <li><strong>Description:</strong> {league.description}</li>
            <li><strong>Pick Type:</strong> {league.pickType}</li>
            <li><strong>Tiebreaker:</strong> {league.tiebreakerType}</li>
            <li><strong>Tie Policy:</strong> {league.tiebreakerTiePolicy}</li>
            <li><strong>Confidence Points:</strong> {league.useConfidencePoints ? "Yes" : "No"}</li>
            <li><strong>Ranking Filter:</strong> {league.rankingFilter || "None"}</li>
            <li><strong>League Window:</strong> {windowLabel}</li>
            <li><strong>Visibility:</strong> {league.isPublic ? "Public" : "Private"}</li>
            <li><strong>Conferences:</strong> {
              (() => {
                const slugs = Array.isArray(league.conferenceSlugs) ? league.conferenceSlugs : [];
                const deduped = [...new Set(slugs)];
                return deduped.length > 0 ? deduped.join(", ") : "None";
              })()
            }</li>
          </ul>
        </div>
      </div>

      <div className="league-detail-sidebar">
        <div className="members-section">
          <h2>Members</h2>
          {league.members?.length > 0 ? (
            <ul className="members-list">
              {league.members.map((member) => (
                <li key={member.userId}>
                  <span className="member-username">{member.username}</span>
                  <span className={`member-role ${member.role}`}>{member.role}</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="no-members-message">No members yet.</p>
          )}
        </div>

        <LeagueInvitation leagueId={league.id} leagueName={league.name} />

        {isCommissioner && (
          <div className="danger-zone">
          <h2>Danger Zone</h2>
          {confirmingDelete ? (
            <>
              <p>Are you sure you want to delete this league? This cannot be undone.</p>
              <button
                className="confirm-delete-button"
                onClick={handleDelete}
                disabled={deleting}
              >
                {deleting ? "Deleting..." : "Yes, Delete League"}
              </button>
              <button className="cancel-button" onClick={() => setConfirmingDelete(false)}>
                Cancel
              </button>
            </>
          ) : (
            <button className="delete-button" onClick={() => setConfirmingDelete(true)}>
              Delete League
            </button>
          )}
          </div>
        )}
      </div>
    </div>
  );
};

export default LeagueDetail;
