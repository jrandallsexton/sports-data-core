// src/api/playerPickemApi.js
//
// Player Pick'em roster-builder data layer.
//
// MOCK-BACKED FOR NOW. The response shape below is the contract the real
// Producer query (athletes-by-position with opponent + opponent-defense
// join) will serve via the API proxy. When that endpoint lands, delete
// MOCK_ATHLETES and the fake-latency wrapper and switch getAthletesByPosition
// to apiClient.get(`/api/${sport}/${league}/pickem/athletes?position=${pos}`)
// — the page consumes only this module, so nothing else changes.
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
//                        pass yds allowed/G for QB/WR/TE, rush yds
//                        allowed/G for RB, points allowed/G for K.
//                        Prior-season values until current-season games
//                        exist.
//   currentSeason        season block or null (null before the athlete's
//                        first game of the season — week 1 everywhere)
//   previousSeason       season block or null (true freshmen)
//
// Season block: { seasonYear, gamesPlayed, stats } where stats carries the
// position-relevant keys (see gridColumns.js). Both seasons use the SAME
// shape so the grid renders prior season directly beneath current season
// in the same columns.
//
// The mock below is a plausible WEEK 5 snapshot so the with-data design
// is visible; Nussmeier carries currentSeason: null to exercise the
// hasn't-played rendering (em-dashes, sinks under stat sorts).

const MOCK_ATHLETES = {
  QB: [
    {
      athleteId: 'ea6ec41d-31a1-623e-1a68-d21910f17bb8', firstName: 'Arch', lastName: 'Manning',
      teamName: 'Texas Longhorns', teamSlug: 'texas-longhorns', position: 'QB',
      opponentName: 'Oklahoma Sooners', opponentSlug: 'oklahoma-sooners', opponentDefPerGame: 158.9,
      currentSeason: { seasonYear: 2026, gamesPlayed: 4, stats: { cmpPct: 68.6, passYds: 1247, passYdsPerGame: 311.8, passTd: 11, interceptions: 2, rushYds: 186 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 12, stats: { cmpPct: 61.2, passYds: 1785, passYdsPerGame: 148.8, passTd: 15, interceptions: 4, rushYds: 224 } },
    },
    {
      athleteId: '4afaf7e4-f027-c0ea-a5a1-358f3730f057', firstName: 'Sam', lastName: 'Leavitt',
      teamName: 'Arizona State Sun Devils', teamSlug: 'arizona-state-sun-devils', position: 'QB',
      opponentName: 'Utah Utes', opponentSlug: 'utah-utes', opponentDefPerGame: 171.2,
      currentSeason: { seasonYear: 2026, gamesPlayed: 4, stats: { cmpPct: 64.1, passYds: 1102, passYdsPerGame: 275.5, passTd: 9, interceptions: 3, rushYds: 147 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 13, stats: { cmpPct: 61.7, passYds: 2885, passYdsPerGame: 221.9, passTd: 24, interceptions: 6, rushYds: 443 } },
    },
    {
      athleteId: 'mock-qb-3', firstName: 'LaNorris', lastName: 'Sellers',
      teamName: 'South Carolina Gamecocks', teamSlug: 'south-carolina-gamecocks', position: 'QB',
      opponentName: 'Kentucky Wildcats', opponentSlug: 'kentucky-wildcats', opponentDefPerGame: 226.8,
      currentSeason: { seasonYear: 2026, gamesPlayed: 5, stats: { cmpPct: 66.9, passYds: 1310, passYdsPerGame: 262.0, passTd: 10, interceptions: 2, rushYds: 312 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 13, stats: { cmpPct: 65.6, passYds: 2534, passYdsPerGame: 194.9, passTd: 18, interceptions: 7, rushYds: 674 } },
    },
    {
      athleteId: 'mock-qb-4', firstName: 'Garrett', lastName: 'Nussmeier',
      teamName: 'LSU Tigers', teamSlug: 'lsu-tigers', position: 'QB',
      opponentName: 'Ole Miss Rebels', opponentSlug: 'ole-miss-rebels', opponentDefPerGame: 243.5,
      // Hasn't played this season (injury) — exercises the null-current
      // rendering and stat-sort sinking.
      currentSeason: null,
      previousSeason: { seasonYear: 2025, gamesPlayed: 13, stats: { cmpPct: 64.2, passYds: 4052, passYdsPerGame: 311.7, passTd: 29, interceptions: 12, rushYds: 34 } },
    },
    {
      athleteId: 'mock-qb-5', firstName: 'Cade', lastName: 'Klubnik',
      teamName: 'Clemson Tigers', teamSlug: 'clemson-tigers', position: 'QB',
      opponentName: null, opponentSlug: null, opponentDefPerGame: null, // bye
      currentSeason: { seasonYear: 2026, gamesPlayed: 4, stats: { cmpPct: 65.8, passYds: 1042, passYdsPerGame: 260.5, passTd: 8, interceptions: 1, rushYds: 122 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 14, stats: { cmpPct: 63.4, passYds: 3639, passYdsPerGame: 259.9, passTd: 36, interceptions: 6, rushYds: 463 } },
    },
  ],
  RB: [
    {
      athleteId: 'mock-rb-1', firstName: 'Jeremiyah', lastName: 'Love',
      teamName: 'Notre Dame Fighting Irish', teamSlug: 'notre-dame-fighting-irish', position: 'RB',
      opponentName: 'USC Trojans', opponentSlug: 'usc-trojans', opponentDefPerGame: 148.6,
      currentSeason: { seasonYear: 2026, gamesPlayed: 4, stats: { rushAtt: 71, rushYds: 512, rushYdsPerGame: 128.0, rushTd: 7, receptions: 11 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 13, stats: { rushAtt: 163, rushYds: 1125, rushYdsPerGame: 86.5, rushTd: 17, receptions: 28 } },
    },
    {
      athleteId: 'mock-rb-2', firstName: 'Nicholas', lastName: 'Singleton',
      teamName: 'Penn State Nittany Lions', teamSlug: 'penn-state-nittany-lions', position: 'RB',
      opponentName: 'UCLA Bruins', opponentSlug: 'ucla-bruins', opponentDefPerGame: 176.3,
      currentSeason: { seasonYear: 2026, gamesPlayed: 5, stats: { rushAtt: 82, rushYds: 464, rushYdsPerGame: 92.8, rushTd: 6, receptions: 14 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 16, stats: { rushAtt: 172, rushYds: 1099, rushYdsPerGame: 68.7, rushTd: 12, receptions: 41 } },
    },
    {
      athleteId: 'mock-rb-3', firstName: 'Makhi', lastName: 'Hughes',
      teamName: 'Oregon Ducks', teamSlug: 'oregon-ducks', position: 'RB',
      opponentName: 'Indiana Hoosiers', opponentSlug: 'indiana-hoosiers', opponentDefPerGame: 101.9,
      currentSeason: { seasonYear: 2026, gamesPlayed: 5, stats: { rushAtt: 96, rushYds: 538, rushYdsPerGame: 107.6, rushTd: 5, receptions: 9 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 13, stats: { rushAtt: 245, rushYds: 1401, rushYdsPerGame: 107.8, rushTd: 15, receptions: 24 } },
    },
  ],
  WR: [
    {
      athleteId: 'mock-wr-1', firstName: 'Jeremiah', lastName: 'Smith',
      teamName: 'Ohio State Buckeyes', teamSlug: 'ohio-state-buckeyes', position: 'WR',
      opponentName: 'Illinois Fighting Illini', opponentSlug: 'illinois-fighting-illini', opponentDefPerGame: 201.7,
      currentSeason: { seasonYear: 2026, gamesPlayed: 5, stats: { receptions: 34, recYds: 587, recYdsPerGame: 117.4, recTd: 7 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 16, stats: { receptions: 76, recYds: 1315, recYdsPerGame: 82.2, recTd: 15 } },
    },
    {
      athleteId: 'mock-wr-2', firstName: 'Ryan', lastName: 'Williams',
      teamName: 'Alabama Crimson Tide', teamSlug: 'alabama-crimson-tide', position: 'WR',
      opponentName: 'Vanderbilt Commodores', opponentSlug: 'vanderbilt-commodores', opponentDefPerGame: 233.1,
      currentSeason: { seasonYear: 2026, gamesPlayed: 4, stats: { receptions: 21, recYds: 356, recYdsPerGame: 89.0, recTd: 4 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 13, stats: { receptions: 48, recYds: 865, recYdsPerGame: 66.5, recTd: 8 } },
    },
    {
      athleteId: 'mock-wr-3', firstName: 'Antonio', lastName: 'Williams',
      teamName: 'Clemson Tigers', teamSlug: 'clemson-tigers', position: 'WR',
      opponentName: null, opponentSlug: null, opponentDefPerGame: null, // bye with Clemson
      currentSeason: { seasonYear: 2026, gamesPlayed: 4, stats: { receptions: 26, recYds: 401, recYdsPerGame: 100.3, recTd: 3 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 13, stats: { receptions: 75, recYds: 904, recYdsPerGame: 69.5, recTd: 11 } },
    },
  ],
  TE: [
    {
      athleteId: 'mock-te-1', firstName: 'Max', lastName: 'Klare',
      teamName: 'Ohio State Buckeyes', teamSlug: 'ohio-state-buckeyes', position: 'TE',
      opponentName: 'Illinois Fighting Illini', opponentSlug: 'illinois-fighting-illini', opponentDefPerGame: 201.7,
      currentSeason: { seasonYear: 2026, gamesPlayed: 5, stats: { receptions: 22, recYds: 264, recYdsPerGame: 52.8, recTd: 3 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 12, stats: { receptions: 51, recYds: 685, recYdsPerGame: 57.1, recTd: 4 } },
    },
    {
      athleteId: 'mock-te-2', firstName: 'Eli', lastName: 'Stowers',
      teamName: 'Vanderbilt Commodores', teamSlug: 'vanderbilt-commodores', position: 'TE',
      opponentName: 'Alabama Crimson Tide', opponentSlug: 'alabama-crimson-tide', opponentDefPerGame: 187.4,
      currentSeason: { seasonYear: 2026, gamesPlayed: 5, stats: { receptions: 25, recYds: 301, recYdsPerGame: 60.2, recTd: 4 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 13, stats: { receptions: 49, recYds: 638, recYdsPerGame: 49.1, recTd: 5 } },
    },
  ],
  K: [
    {
      athleteId: 'mock-k-1', firstName: 'Dominic', lastName: 'Zvada',
      teamName: 'Michigan Wolverines', teamSlug: 'michigan-wolverines', position: 'K',
      opponentName: 'Washington Huskies', opponentSlug: 'washington-huskies', opponentDefPerGame: 22.6,
      currentSeason: { seasonYear: 2026, gamesPlayed: 5, stats: { fgMade: 9, fgAtt: 10, fgPct: 90.0, fgLong: 56, xpMade: 14, xpAtt: 14 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 13, stats: { fgMade: 21, fgAtt: 22, fgPct: 95.5, fgLong: 56, xpMade: 38, xpAtt: 38 } },
    },
    {
      athleteId: 'mock-k-2', firstName: 'Peyton', lastName: 'Woodring',
      teamName: 'Georgia Bulldogs', teamSlug: 'georgia-bulldogs', position: 'K',
      opponentName: 'Auburn Tigers', opponentSlug: 'auburn-tigers', opponentDefPerGame: 19.8,
      currentSeason: { seasonYear: 2026, gamesPlayed: 5, stats: { fgMade: 8, fgAtt: 9, fgPct: 88.9, fgLong: 52, xpMade: 17, xpAtt: 17 } },
      previousSeason: { seasonYear: 2025, gamesPlayed: 14, stats: { fgMade: 19, fgAtt: 23, fgPct: 82.6, fgLong: 49, xpMade: 46, xpAtt: 46 } },
    },
  ],
};

// Small fake latency so loading states are exercised the way the real
// endpoint will exercise them.
function respond(rows) {
  return new Promise((resolve) => {
    setTimeout(() => resolve({ data: { athletes: rows } }), 250);
  });
}

const PlayerPickemApi = {
  // position: "QB" | "RB" | "WR" | "TE" | "K". FLEX is a UI concept —
  // the page requests each eligible position and merges.
  getAthletesByPosition: (sport, league, position) =>
    respond(MOCK_ATHLETES[position] ?? []),
};

export default PlayerPickemApi;
