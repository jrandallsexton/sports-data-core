// src/components/leagues/Leagues.jsx
import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import LeaguesApi from "api/leagues/leaguesApi";
import LeagueOverviewCard from "./LeagueOverviewCard";
import "./Leagues.css"; // for grid styling

const Leagues = () => {
  const [leagues, setLeagues] = useState([]);

  useEffect(() => {
    const fetchLeagues = async () => {
      const result = await LeaguesApi.getUserLeagues();
      setLeagues(result);
    };
    fetchLeagues();
  }, []);

  return (
    <div className="page-container">
      <div className="leagues-header">
        <h1>My Leagues</h1>
        <Link to="/app/league/create" className="create-league-button">
          + Create League
        </Link>
      </div>
      {leagues.length === 0 ? (
        <p>
          You’re not part of any leagues yet.{" "}
          <Link to="/app/league/create">Create one</Link>.
        </p>
      ) : (
        <div className="league-grid">
          {leagues.map((league) => (
            <LeagueOverviewCard key={league.id} league={league} />
          ))}
        </div>
      )}
    </div>
  );
};

export default Leagues;
