import "./HomePage.css";
import { useUserDto } from "../../contexts/UserContext";
import PrimarySlotNewUser from "./PrimarySlotNewUser";
import PrimarySlotOffSeasonCountdown from "./PrimarySlotOffSeasonCountdown";
import YourLeaguesCard from "./YourLeaguesCard";

/**
 * Post-login landing — date-aware, segment-aware.
 *
 * Design reference: docs/post-login-landing-design.md
 *
 * Three tiers:
 *   Tier 1 (primary) — the single "next action" for this user.
 *   Tier 2 (context) — sport-specific context cards. Stubbed this session.
 *   Tier 3 (secondary) — compact adjacent surfaces. Stubbed this session.
 *
 * Session 1 ships Tier 1 with two of the seven rules wired up:
 *   - New user (no leagues)       → PrimarySlotNewUser
 *   - Fallback (any other state)  → PrimarySlotOffSeasonCountdown
 *
 * Covers every real user today (April 2026: NCAAFB + NFL are off-season;
 * MLB is dev-only, not product-facing). Remaining rules — pick deadline
 * within 48h, new matchups available, standings delta, commissioner
 * action pending, welcome-back fallback — land in session 2 once we
 * have per-league deadline data plumbed through /user/me or a new
 * dashboard endpoint.
 */
function HomePage() {
  const { userDto, loading: userLoading } = useUserDto();

  if (userLoading) {
    return <div className="home-page home-page--loading">Loading…</div>;
  }

  // Normalize defensively — BE may serialize leagues as either an array
  // or an id-keyed object. Same handling as MessageboardPage and
  // YourLeaguesCard. Without this, an id-keyed payload would route a
  // real user to PrimarySlotNewUser and skip the Tier 2 list.
  const leagues = Array.isArray(userDto?.leagues)
    ? userDto.leagues
    : Object.values(userDto?.leagues || {});
  const hasLeagues = leagues.length > 0;

  // Rule resolver for Tier 1. Add cases here as session 2 lands — pick
  // deadlines, standings deltas, etc. — keeping the cascade top-down so
  // the most-urgent state always wins.
  const renderPrimary = () => {
    if (!hasLeagues) return <PrimarySlotNewUser />;
    return <PrimarySlotOffSeasonCountdown />;
  };

  return (
    <div className="home-page">
      <section className="home-tier home-tier--primary">
        {renderPrimary()}
      </section>

      {/* Tier 2 — "Your Leagues" list. Mirrors sd-mobile's YourLeaguesCard
          so logged-in users with leagues see them on the landing page
          (regression vs. mobile noted 2026-05-03). Hidden for zero-league
          users — that branch resolves to PrimarySlotNewUser above. */}
      {hasLeagues && (
        <section className="home-tier home-tier--context">
          <YourLeaguesCard />
        </section>
      )}

      {/* Tier 3 — compact secondary row. Stub; session 3. */}
      <section className="home-tier home-tier--secondary home-tier--stub" aria-hidden="true" />
    </div>
  );
}

export default HomePage;
