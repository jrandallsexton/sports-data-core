// Canonical league-rooted picks URLs. ONE route family serves both games
// (team pick'em and Player Pick'em) — the league's GroupType decides what
// renders (LeaguePicksRouter), so no link-builder ever needs to know what
// game a league plays.
//
// Phase is part of every canonical week URL because week NUMBERS repeat
// across season phases (NFL: preseason/regular/postseason each count
// from 1) — "/weeks/4" alone is ambiguous. Phases are slugs sourced from
// the league's seasonWeekDetails payload: "preseason" | "regular" |
// "postseason" | "offseason". Phase-less URLs are accepted and redirect
// to canonical once the week's phase resolves (PicksPage week-snap).

export const leaguePicksPath = (leagueId, week, phase = "regular") =>
  week == null
    ? `/app/league/${leagueId}/picks`
    : `/app/league/${leagueId}/picks/phase/${phase}/weeks/${week}`;

// League-less nav landing: LeaguePicksRouter resolves the remembered (or
// first) league and redirects to leaguePicksPath.
export const picksLandingPath = "/app/picks";
