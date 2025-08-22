// src/components/leagues/LeagueOverviewCard.jsx
import { Link } from "react-router-dom";
import "./LeagueOverviewCard.css"; // Assuming you have styles for the card

const LeagueOverviewCard = ({ league }) => {
  return (
    <div className="card">
      {league.avatarUrl && (
        <img
          src={league.avatarUrl}
          alt={`${league.name} avatar`}
          className="league-avatar"
        />
      )}
      <h2>{league.name}</h2>
      <p>
        <strong>Type:</strong> {league.leagueType}
      </p>
      <p>
        <strong>Confidence Points:</strong> {league.useConfidencePoints == true ? "Yes" : "No"}
      </p>
      <p>
        <strong>Members:</strong> {league.memberCount}
      </p>
      <Link to={`/app/league/${league.id}`} className="submit-button">
        League Settings
      </Link>
      <Link to={`/app/picks/${league.id}`} className="submit-button">
        Picks
      </Link>
    </div>
  );
};

export default LeagueOverviewCard;
