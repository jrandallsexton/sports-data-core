// src/api/playerPickemApi.js
//
// Player Pick'em roster-builder data layer, served by the API's relay to
// Producer's athletes/pickem feed.
//
// Row contract (one per athlete):
//   athleteId            Guid
//   firstName, lastName  identity (grid default-sorts by lastName)
//   teamName             franchise display name (text only — no marks/logos)
//   teamSlug             for linking to the team card
//   position             "QB" | "RB" | "WR" | "TE" | "K"
//   opponentName         this week's opponent (null on bye)
//   opponentSlug         opponent team-card link (null on bye)
//   opponentDefPerGame   opponent's relevant defensive allowance per game:
//                        net pass yds allowed/G for QB/WR/TE, rush yds
//                        allowed/G for RB, points allowed/G for K —
//                        aggregated server-side from what the opponent's
//                        opponents actually gained; prior-season values
//                        until the opponent has current-season games.
//   currentSeason        { seasonYear, gamesPlayed, stats } or null (null
//                        before the athlete's first game — week 1 everywhere)
//   previousSeason       same shape or null (true freshmen)
//
// stats keys are position-scoped (cmpPct, passYds, rushAtt, receptions,
// fgMade, ...) — see gridColumns.js for the render mapping.

import apiClient from './apiClient';

const PlayerPickemApi = {
  // position: "QB" | "RB" | "WR" | "TE" | "K". FLEX is a UI concept —
  // the page requests each eligible position and merges.
  getAthletesByPosition: (sport, league, position, seasonYear, week) =>
    apiClient.get(
      `/api/${sport}/${league}/athletes/pickem?position=${encodeURIComponent(position)}&seasonYear=${seasonYear}&week=${week}`
    ),
};

export default PlayerPickemApi;
