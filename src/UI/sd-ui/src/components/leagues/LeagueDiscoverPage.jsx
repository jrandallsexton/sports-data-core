import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import LeaguesApi from "api/leagues/leaguesApi";

function LeagueDiscoverPage() {
  const [leagues, setLeagues] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    LeaguesApi.getPublicLeagues()
      .then((data) => {
        setLeagues(data || []);
      })
      .catch((err) => {
        console.error("Failed to load public leagues:", err);
      })
      .finally(() => setLoading(false));
  }, []);

  return (
    <div>
      <h2>Discover Public Leagues</h2>
      {loading ? (
        <p>Loading leagues...</p>
      ) : leagues.length === 0 ? (
        <p>No public leagues available right now.</p>
      ) : (
        <ul>
          {leagues.map((league) => (
            <li key={league.id} style={{ marginBottom: "1rem" }}>
              <strong>{league.name}</strong> <br />
              <span>Commissioner: {league.commissioner}</span> <br />
              <span>{league.description}</span> <br />
              <Link to={`/join/${league.id.replace(/-/g, "")}`}>
                Join This League
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export default LeagueDiscoverPage;
