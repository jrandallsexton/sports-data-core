import { Navigate, useParams } from "react-router-dom";
import { useUserDto } from "../contexts/UserContext";
import { useLeagueContext } from "../contexts/LeagueContext";
import PicksPage from "../components/picks/PicksPage.jsx";
import PlayerRosterBuilder, {
  WEEK as PICKEM_WEEK,
} from "../components/pickem/players/PlayerRosterBuilder";
import AdminRoute from "./AdminRoute";
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
function LeaguePicksRouter() {
  const { leagueId, phase, week } = useParams();
  const { userDto, loading } = useUserDto();
  const { selectedLeagueId } = useLeagueContext();

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
    // Canonicalize to the LEAGUE'S current week (phase-qualified, from
    // /user/me) — a preseason-only league lives at its preseason week,
    // not at a pinned default. Fallback covers rollout payloads that
    // predate seasonWeekDetails.
    const details = league.seasonWeekDetails ?? [];
    const current =
      details.find((d) => d.seasonWeekId === league.currentSeasonWeekId) ??
      details[details.length - 1] ??
      { week: PICKEM_WEEK, phase: "regular" };
    if (Number(week) !== current.week || phase !== current.phase) {
      return (
        <Navigate to={leaguePicksPath(leagueId, current.week, current.phase)} replace />
      );
    }
    // Admin-only until Player Pick'em launches (week 3-4 alpha) — same
    // gate the old /pickem/players route carried.
    return (
      <AdminRoute>
        <PlayerRosterBuilder />
      </AdminRoute>
    );
  }

  return <PicksPage />;
}

export default LeaguePicksRouter;
