import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import apiClient from "../../api/apiClient";
import "./TeamCard.css";
import { formatToEasternTime } from "../../utils/timeUtils";

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
            {team.stadiumName} – {team.stadiumCapacity.toLocaleString()} capacity
          </p>
        </div>
      </div>

      <div className="team-news">
        <h3>Latest News</h3>
        <ul>
          {team.news?.length ? (
            team.news.map((item, idx) => (
              <li key={idx}>
                <a href={item.link} target="_blank" rel="noopener noreferrer">
                  {item.title}
                </a>
              </li>
            ))
          ) : (
            <div>No news available.</div>
          )}
        </ul>
      </div>

      <div className="team-schedule">
        <h3>Schedule ({resolvedSeason})</h3>
        <table>
          <thead>
            <tr>
              <th>Kickoff (ET)</th>
              <th>Opponent</th>
              <th>Location</th>
              <th>Result</th>
            </tr>
          </thead>
          <tbody>
            {team.schedule?.length ? (
              team.schedule.map((game, idx) => (
                <tr key={idx}>
                  <td>{formatToEasternTime(game.date)}</td>
                  <td>
                    <Link
                      to={`/app/sport/football/ncaa/team/${game.opponentSlug}/${resolvedSeason}`}
                      className="team-link"
                    >
                      {game.opponent}
                    </Link>
                  </td>
                  <td>{game.location}</td>
                  <td
                    className={
                      game.result.trim().toUpperCase().startsWith("W")
                        ? "result-win"
                        : "result-loss"
                    }
                  >
                    {game.result}
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan="3">No games scheduled.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default TeamCard;
