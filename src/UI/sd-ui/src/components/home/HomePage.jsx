import "./HomePage.css";
import { useUserDto } from "../../contexts/UserContext";
import PrimarySlotOffSeasonCountdown from "./PrimarySlotOffSeasonCountdown";
import PendingInvitesCard from "./PendingInvitesCard";
import YourLeaguesCard from "./YourLeaguesCard";
import JoinableLeaguesCard from "./JoinableLeaguesCard";

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
 * Tier 1 currently resolves to PrimarySlotOffSeasonCountdown for EVERY user.
 * The old zero-league branch (PrimarySlotNewUser) was removed 2026-07-29: it
 * hardcoded the season year and led with "Create a league" — an action the
 * per-sport creation gates block until each sport opens (NCAAFB Aug 18, NFL
 * Sep 1). The countdown is gate-aware and states exactly when each sport
 * opens, so it is correct for members and non-members alike; it takes
 * hasLeagues so its in-season copy can address users who haven't joined one.
 *
 * Remaining rules — pick deadline within 48h, new matchups available,
 * standings delta, commissioner action pending, welcome-back fallback — land
 * once per-league deadline data is plumbed through /user/me or a dashboard
 * endpoint.
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

  return (
    <div className="home-page">
      <section className="home-tier home-tier--primary">
        <PrimarySlotOffSeasonCountdown hasLeagues={hasLeagues} />
      </section>

      {/* Pending league invitations — above the league list because an
          unanswered invite is the most actionable thing on the page. The
          card renders null when there are none. Closes the gap where a
          push notification implied something was waiting in-app. */}
      <section className="home-tier home-tier--context">
        <PendingInvitesCard />
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

      {/* Tier 3 — public-league discovery. THE content driver for
          league-less users: actionable joins instead of a bare countdown.
          The card renders null when nothing is joinable, leaving the
          prior stub behavior. */}
      <section className="home-tier home-tier--secondary">
        <JoinableLeaguesCard />
      </section>
    </div>
  );
}

export default HomePage;
