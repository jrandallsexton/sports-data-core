import { useEffect, useState } from "react";
import { Navigate, useParams } from "react-router-dom";
import { useUserDto } from "../contexts/UserContext";
import { useLeagueContext } from "../contexts/LeagueContext";
import PicksPage from "../components/picks/PicksPage.jsx";
import PlayerRosterBuilder, {
  WEEK as PICKEM_WEEK,
} from "../components/pickem/players/PlayerRosterBuilder";
import { leaguePicksPath } from "./paths";

/**
 * One route family, both games. /app/league/:leagueId/picks[/phase/:phase
 * /weeks/:week] renders whichever surface the league's GroupType calls
 * for — team pick'em (PicksPage) or Player Pick'em (roster builder). One
 * league plays one game (the GroupType enum), so the URL never encodes
 * the game type and link builders never branch on it.
 *
 * The bare /app/picks nav landing also routes here (no :leagueId): the
 * remembered league (LeagueContext, localStorage-backed) or the first
 * active league wins, redirecting to its canonical URL. No leagues at
 * all → PicksPage renders its own empty state.
 *
 * A leagueId that isn't in the active set falls through to PicksPage,
 * which owns the past-league (deactivated) read-only view and the
 * bad-id fallback redirect.
 */
// Bounded wait for a new league's weeks (see awaitingWeeks).
const WEEKS_WAIT_INTERVAL_MS = 1000;
const WEEKS_WAIT_MAX_ATTEMPTS = 10;

function LeaguePicksRouter() {
  const { leagueId, phase, week } = useParams();
  const { userDto, loading, refreshUserDto } = useUserDto();
  const { selectedLeagueId } = useLeagueContext();

  // A just-created Player Pick'em league reaches /user/me before bootstrap
  // has materialized its weeks (~1s, async), so seasonWeekDetails is []. Re-read
  // /user/me briefly instead of canonicalizing to the week-1 fallback; a
  // payload without the field at all (pre-rollout) skips this.
  const pendingLeague = leagueId
    ? (Array.isArray(userDto?.leagues)
        ? userDto.leagues
        : Object.values(userDto?.leagues || {})
      ).find((l) => l.id === leagueId)
    : null;
  const awaitingWeeks =
    pendingLeague?.groupType === "PlayerPickem" &&
    Array.isArray(pendingLeague.seasonWeekDetails) &&
    pendingLeague.seasonWeekDetails.length === 0;
  const [weeksWaitExpired, setWeeksWaitExpired] = useState(false);
  useEffect(() => {
    if (!awaitingWeeks) return undefined;
    let attempts = 0;
    const timer = setInterval(() => {
      attempts += 1;
      refreshUserDto();
      if (attempts >= WEEKS_WAIT_MAX_ATTEMPTS) {
        clearInterval(timer);
        setWeeksWaitExpired(true);
      }
    }, WEEKS_WAIT_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [awaitingWeeks, refreshUserDto]);

  if (loading) {
    return <div className="route-loading">Loading...</div>;
  }

  // BE may return leagues as an array or an id-keyed object — match the
  // defensive shape handling used elsewhere (YourLeaguesCard).
  const leagues = Array.isArray(userDto?.leagues)
    ? userDto.leagues
    : Object.values(userDto?.leagues || {});

  if (!leagueId) {
    const remembered = leagues.find((l) => l.id === selectedLeagueId);
    const target = remembered ?? leagues[0];
    if (target) {
      return <Navigate to={leaguePicksPath(target.id)} replace />;
    }
    // No leagues: PicksPage renders the join-a-league empty state.
    return <PicksPage />;
  }

  const league = leagues.find((l) => l.id === leagueId);

  if (league?.groupType === "PlayerPickem") {
    if (awaitingWeeks && !weeksWaitExpired) {
      return <div className="route-loading">Setting up your league...</div>;
    }
    // Canonicalize to the LEAGUE'S current week (phase-qualified, from
    // /user/me) — a preseason-only league lives at its preseason week,
    // not at a pinned default. Fallback covers rollout payloads that
    // predate seasonWeekDetails.
    const details = league.seasonWeekDetails ?? [];
    const current =
      details.find((d) => d.seasonWeekId === league.currentSeasonWeekId) ??
      details[details.length - 1] ??
      { week: PICKEM_WEEK, phase: "regular" };
    // Redirect only when the URL's (phase, week) isn't one of the league's
    // weeks (or is missing) — the same rule PicksPage applies — so the week
    // selector can move between the league's weeks. The "differs from
    // current" guard keeps an empty week list from redirecting to itself.
    const inLeague = details.some(
      (d) => d.week === Number(week) && d.phase === phase
    );
    if (!inLeague && (Number(week) !== current.week || phase !== current.phase)) {
      return (
        <Navigate to={leaguePicksPath(leagueId, current.week, current.phase)} replace />
      );
    }
    // Open to any member: only league CREATION is admin-only during the
    // alpha (create route + API), so playing a league you joined is allowed.
    return <PlayerRosterBuilder />;
  }

  return <PicksPage />;
}

export default LeaguePicksRouter;
