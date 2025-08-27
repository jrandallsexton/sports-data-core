import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import LeaguesApi from "api/leagues/leaguesApi";
import "./LeagueDiscoverPage.css";

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
    <div className="league-discover-page">
      <h2>Discover Public Leagues</h2>
      
      {loading ? (
        <div className="loading-message">Loading leagues...</div>
      ) : leagues.length === 0 ? (
        <div className="no-leagues-message">No public leagues available right now.</div>
      ) : (
        <div className="leagues-table">
          <div className="table-header">
            <div>League Name</div>
            <div>Commissioner</div>
            <div>Description</div>
            <div>Action</div>
          </div>
          
          {leagues.map((league) => (
            <div key={league.id} className="table-row">
              <div className="league-name">{league.name}</div>
              <div className="commissioner-name">{league.commissioner}</div>
              <div className="league-description">{league.description}</div>
              <div className="join-action">
                <Link 
                  to={`/app/join/${league.id.replace(/-/g, "")}`}
                  className="join-button"
                >
                  Join League
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default LeagueDiscoverPage;
