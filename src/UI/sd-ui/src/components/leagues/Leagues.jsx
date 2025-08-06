import React, { useEffect, useState } from "react";
import LeaguesApi from "api/leagues/leaguesApi";
import { Link } from "react-router-dom";

const Leagues = () => {
  const [leagues, setLeagues] = useState([]);

  useEffect(() => {
    const fetchLeagues = async () => {
      const result = await LeaguesApi.getUserLeagues(); // renamed to match the new function
      setLeagues(result);
    };
    fetchLeagues();
  }, []);

  return (
    <div className="page-container">
      <h1>My Leagues</h1>
      {leagues.length === 0 ? (
        <p>You’re not part of any leagues yet.</p>
      ) : (
        <div className="card-grid">
          {leagues.map((league) => (
            <div key={league.id} className="card">
              {league.avatarUrl && (
                <img
                  src={league.avatarUrl}
                  alt={`${league.name} avatar`}
                  style={{ maxWidth: "100px", marginBottom: "0.5rem" }}
                />
              )}
              <h2>{league.name}</h2>
              <p>
                <strong>Sport:</strong> {league.sport}
              </p>
              <p>
                <strong>Type:</strong> {league.leagueType}
              </p>
              <Link to={`/app/league/${league.id}`} className="submit-button">
                View League
              </Link>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default Leagues;
