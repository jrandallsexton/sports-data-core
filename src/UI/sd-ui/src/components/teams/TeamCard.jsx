import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import apiClient from "../../api/apiClient";
import "./TeamCard.css";
import TeamSchedule from "./TeamSchedule";
import TeamNews from "./TeamNews";

function TeamCard() {
  const { slug, seasonYear } = useParams();
  const [team, setTeam] = useState(null);
  const [loading, setLoading] = useState(true);

  const resolvedSeason = seasonYear || new Date().getFullYear();

  useEffect(() => {
    const fetchTeam = async () => {
      try {
        const response = await apiClient.get(
          `/ui/teamcard/sport/football/league/ncaa/team/${slug}/${resolvedSeason}`
        );
        setTeam(response.data);
      } catch (error) {
        console.error("Failed to fetch team data:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchTeam();
  }, [slug, resolvedSeason]);

  if (loading) return <div className="team-card">Loading team data…</div>;
  if (!team) return <div className="team-card">Team not found.</div>;

  return (
    <div
      className="team-card"
      style={{
        "--accent-color": team.colorPrimary || "#6c63ff",
        "--highlight-color": team.colorSecondary || "#ffc107",
      }}
    >
      <div className="team-header">
        <img
          src={team.logoUrl}
          alt={`${team.name} logo`}
          className="team-logo"
        />
        <div>
          <h2 className="team-name">{team.name}</h2>
          <p className="team-location">{team.location}</p>
          <p className="team-stadium">
            {team.stadiumName} – {team.stadiumCapacity.toLocaleString()}{" "}
            capacity
          </p>
        </div>
      </div>

      <TeamNews news={team.news} />

      <TeamSchedule schedule={team.schedule} seasonYear={resolvedSeason} />
    </div>
  );
}

export default TeamCard;
