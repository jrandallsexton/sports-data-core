// src/components/leagues/LeagueOverviewCard.jsx
import { Link } from "react-router-dom";
import "./LeagueOverviewCard.css"; // Assuming you have styles for the card

const LeagueOverviewCard = ({ league, onDuplicate }) => {
  // A deactivated league is a finished season: read-only, and not cloneable
  // (CloneLeagueCommandHandler rejects it server-side regardless).
  const isPast = !!league.deactivatedUtc;

  return (
    <div className={`card${isPast ? " card-past" : ""}`}>
      {league.avatarUrl && (
        <img
          src={league.avatarUrl}
          alt={`${league.name} avatar`}
          className="league-avatar"
        />
      )}
      <h2 className="league-card-title">
        <Link to={`/app/picks/${league.id}`} className="league-card-name-link">
          {league.name}
        </Link>
        {isPast && <span className="past-league-badge">Past</span>}
        {league.description && (
          <span className="league-card-description">{league.description}</span>
        )}
      </h2>
      <p>
        <strong>Type:</strong> {league.leagueType}
      </p>
      <p>
        <strong>Confidence Points:</strong> {league.useConfidencePoints === true ? "Yes" : "No"}
      </p>
      <p>
        <strong>Members:</strong> {league.memberCount}
      </p>
      <div className="league-card-actions">
        <Link to={`/app/league/${league.id}`} className="submit-button">
          Settings
        </Link>
        {onDuplicate && !isPast && (
          <button
            type="button"
            className="submit-button"
            onClick={() => onDuplicate(league)}
          >
            Duplicate
          </button>
        )}
      </div>
    </div>
  );
};

export default LeagueOverviewCard;
