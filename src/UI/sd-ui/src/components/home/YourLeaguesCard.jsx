import { Link } from "react-router-dom";
import { useUserDto } from "../../contexts/UserContext";
import "./YourLeaguesCard.css";

/**
 * Tier 2 — "Your Leagues" card. Web mirror of sd-mobile's YourLeaguesCard
 * (src/UI/sd-mobile/src/components/features/home/YourLeaguesCard.tsx).
 * Lists the user's active leagues below the Tier 1 primary slot. Each row
 * deep-links to the picks screen with that league preselected
 * (/app/picks/:leagueId — see MainApp route).
 *
 * Hidden when the user has zero leagues; HomePage already routes that
 * branch to PrimarySlotNewUser.
 */
function YourLeaguesCard() {
  const { userDto } = useUserDto();

  // BE may return leagues as an array or an id-keyed object — match the
  // defensive shape handling used elsewhere (MessageboardPage).
  const leagues = Array.isArray(userDto?.leagues)
    ? userDto.leagues
    : Object.values(userDto?.leagues || {});

  if (leagues.length === 0) return null;

  return (
    <div className="your-leagues-card">
      <div className="your-leagues-card__eyebrow">YOUR LEAGUES</div>
      <ul className="your-leagues-card__list">
        {leagues.map((league) => (
          <li key={league.id} className="your-leagues-card__item">
            <Link
              to={`/app/picks/${league.id}`}
              className="your-leagues-card__row"
              aria-label={`Open ${league.name}`}
            >
              <span className="your-leagues-card__name">{league.name}</span>
              <span className="your-leagues-card__chevron" aria-hidden="true">
                ›
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}

export default YourLeaguesCard;
