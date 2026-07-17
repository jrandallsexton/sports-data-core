import { Link } from "react-router-dom";
import { useUserDto } from "../../contexts/UserContext";
import "./YourLeaguesCard.css";

// Default sport-icon glyphs. Stand-in until commissioner-uploaded league
// icons land — at that point the per-league icon overrides this map and
// the unknown-sport branch becomes the fallback for legacy rows.
const SPORT_ICON = {
  FootballNcaa: "🏈",
  FootballNfl: "🏈",
  BaseballMlb: "⚾",
};

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
      <div className="your-leagues-card__header">
        <div className="your-leagues-card__eyebrow">YOUR LEAGUES</div>
        {/* Mirrors sd-mobile's "Manage ›" affordance — routes to the leagues
            management page (clone, filters, past leagues). */}
        <Link to="/app/leagues" className="your-leagues-card__manage">
          Manage ›
        </Link>
      </div>
      <ul className="your-leagues-card__list">
        {leagues.map((league) => {
          const icon = SPORT_ICON[league.sport];
          return (
            <li key={league.id} className="your-leagues-card__item">
              <Link
                to={`/app/picks/${league.id}`}
                className="your-leagues-card__row"
                aria-label={`Open ${league.name}`}
              >
                {/* Always render the icon span — its CSS min-width reserves
                    the column even when icon is undefined (unknown Sport
                    enum value, or pre-rollout cached /me without the Sport
                    field). Skipping the span entirely produces a mid-rollout
                    visual jitter where mixed rows shift the name column. */}
                <span className="your-leagues-card__icon" aria-hidden="true">
                  {icon}
                </span>
                <span className="your-leagues-card__text">
                  <span className="your-leagues-card__name">{league.name}</span>
                  {league.description && (
                    <span className="your-leagues-card__description">
                      {league.description}
                    </span>
                  )}
                </span>
                <span className="your-leagues-card__chevron" aria-hidden="true">
                  ›
                </span>
              </Link>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

export default YourLeaguesCard;
