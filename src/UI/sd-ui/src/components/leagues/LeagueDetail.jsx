import React, { useEffect, useState } from "react";
import { useUserDto } from "../../contexts/UserContext";
import { useParams, useNavigate } from "react-router-dom";
import leaguesApi from "../../api/leagues/leaguesApi";
import "./LeagueDetail.css";
import LeagueInvitation from "./LeagueInvitation";

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
      alert("Error deleting league. Please try again.");
    } finally {
      setDeleting(false);
    }
  };

  if (loading || userLoading) return <p>Loading league details...</p>;
  if (!league) return <p>League not found.</p>;

  const commissioner = league.members.find((m) => m.role === "commissioner");
  const commissionerId = commissioner?.userId;
  const isCommissioner = userDto?.id === commissionerId;

  return (
    <div className="page-container">
      <h2>{league.name}</h2>
      <ul>
        <li><strong>Description:</strong> {league.description}</li>
        <li><strong>Pick Type:</strong> {league.pickType}</li>
        <li><strong>Tiebreaker:</strong> {league.tiebreakerType}</li>
        <li><strong>Tie Policy:</strong> {league.tiebreakerTiePolicy}</li>
        <li><strong>Confidence Points:</strong> {league.useConfidencePoints ? "Yes" : "No"}</li>
        <li><strong>Ranking Filter:</strong> {league.rankingFilter || "None"}</li>
        <li><strong>Visibility:</strong> {league.isPublic ? "Public" : "Private"}</li>
        <li><strong>Conferences:</strong> {
          [...new Set(league.conferenceSlugs)]?.join(", ") || "None"
        }</li>
      </ul>

      <div>
        <h2>Members</h2>
        {league.members?.length > 0 ? (
          <ul>
            {league.members.map((member) => (
              <li key={member.userId}>
                {member.username} ({member.role})
              </li>
            ))}
          </ul>
        ) : (
          <p>No members yet.</p>
        )}
      </div>

      <LeagueInvitation leagueId={league.id} leagueName={league.name} />

      {isCommissioner && (
        <div className="danger-zone">
          <h2 style={{ color: "red" }}>Danger Zone</h2>
          {confirmingDelete ? (
            <>
              <p>Are you sure you want to delete this league? This cannot be undone.</p>
              <button
                className="confirm-delete-button submit-button"
                onClick={handleDelete}
                disabled={deleting}
              >
                {deleting ? "Deleting..." : "Yes, Delete League"}
              </button>
              <button onClick={() => setConfirmingDelete(false)}>Cancel</button>
            </>
          ) : (
            <button className="delete-button" onClick={() => setConfirmingDelete(true)}>
              Delete League
            </button>
          )}
        </div>
      )}
    </div>
  );
};

export default LeagueDetail;
