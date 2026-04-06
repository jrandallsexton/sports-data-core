import { useEffect, useState } from "react";
import apiWrapper from "../../api/apiWrapper";
import "./TeamRoster.css";

function TeamRoster({ slug, seasonYear, sport, league }) {
  const [roster, setRoster] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchRoster = async () => {
      setLoading(true);
      setError(null);
      try {
        const response = await apiWrapper.TeamCard.getRoster(sport, league, slug, seasonYear);
        setRoster(response.data?.value?.players ?? response.data?.players ?? []);
      } catch (err) {
        console.error("Failed to fetch roster:", err);
        setError("Failed to load roster");
      } finally {
        setLoading(false);
      }
    };

    if (slug && seasonYear) {
      fetchRoster();
    } else {
      setLoading(false);
    }
  }, [sport, league, slug, seasonYear]);

  if (loading) return <div className="team-roster">Loading roster...</div>;
  if (error) return <div className="team-roster error">{error}</div>;
  if (!roster || roster.length === 0)
    return <div className="team-roster">No roster data available.</div>;

  return (
    <div className="team-roster">
      <table className="roster-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Name</th>
            <th>Pos</th>
            <th>Ht</th>
            <th>Wt</th>
            <th>Exp</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {roster.map((player) => (
            <tr key={player.athleteSeasonId}>
              <td>{player.jersey ?? "-"}</td>
              <td>{player.displayName ?? player.shortName ?? "-"}</td>
              <td title={player.position}>{player.positionAbbreviation ?? player.position ?? "-"}</td>
              <td>{player.heightDisplay ?? "-"}</td>
              <td>{player.weightDisplay ?? "-"}</td>
              <td title={player.experienceDisplayValue}>
                {player.experienceYears != null ? `${player.experienceYears} yr` : "-"}
              </td>
              <td>
                <span className={`status-badge ${player.isActive ? "active" : "inactive"}`}>
                  {player.isActive ? "Active" : "Inactive"}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default TeamRoster;
