import React, { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import leaguesApi from "../../api/leagues/leaguesApi";
import "./LeagueDetail.css";
import LeagueInvitation from "./LeagueInvitation";

const LeagueDetail = () => {
  const { id } = useParams();
  const [league, setLeague] = useState(null);
  const [loading, setLoading] = useState(true);

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

  if (loading) return <p>Loading league details...</p>;
  if (!league) return <p>League not found.</p>;

  return (
    <div className="page-container">
      <h2>{league.name}</h2>
      <ul>
        <li>
          <strong>Description:</strong> {league.description}
        </li>
        <li>
          <strong>Pick Type:</strong> {league.pickType}
        </li>
        <li>
          <strong>Tiebreaker:</strong> {league.tiebreakerType}
        </li>
        <li>
          <strong>Tie Policy:</strong> {league.tiebreakerTiePolicy}
        </li>
        <li>
          <strong>Confidence Points:</strong>{" "}
          {league.useConfidencePoints ? "Yes" : "No"}
        </li>
        <li>
          <strong>Ranking Filter:</strong> {league.rankingFilter || "None"}
        </li>
        <li>
          <strong>Visibility:</strong> {league.isPublic ? "Public" : "Private"}
        </li>
        <li>
          <strong>Conferences:</strong>{" "}
          {league.conferenceSlugs?.length > 0
            ? league.conferenceSlugs.join(", ")
            : "None"}
        </li>
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

    </div>
  );
};

export default LeagueDetail;
