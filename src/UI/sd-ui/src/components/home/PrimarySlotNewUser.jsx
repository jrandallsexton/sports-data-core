import { Link } from "react-router-dom";

/**
 * Tier 1 primary slot — shown when the signed-in user has no league
 * memberships yet. Single, unambiguous CTA into league creation.
 *
 * Part of the date-aware / segment-aware landing design in
 * docs/post-login-landing-design.md.
 */
function PrimarySlotNewUser() {
  return (
    <div className="home-primary home-primary--new-user">
      <div className="home-primary__eyebrow">Welcome to sportDeets</div>
      <h1 className="home-primary__headline">Pick the 2026 season with friends</h1>
      <p className="home-primary__body">
        Start your own pick'em league in under a minute, or browse public leagues
        to join one.
      </p>
      <div className="home-primary__actions">
        <Link to="/app/league/create" className="home-primary__cta home-primary__cta--primary">
          Create a league
        </Link>
        <Link to="/app/league/discover" className="home-primary__cta home-primary__cta--secondary">
          Browse public leagues
        </Link>
      </div>
    </div>
  );
}

export default PrimarySlotNewUser;
