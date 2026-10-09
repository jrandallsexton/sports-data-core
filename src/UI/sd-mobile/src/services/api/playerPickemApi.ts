// Player Pick'em roster-builder data layer — TS mirror of sd-ui's
// src/api/playerPickemApi.js, served by the API's relay to Producer's
// athletes/pickem feed.

import { apiClient } from './client';

export type SeasonBlock = {
  seasonYear: number;
  gamesPlayed: number;
  stats: Record<string, number>;
};

export type PickemAthlete = {
  athleteId: string;
  // The athlete-season row a lineup slot persists against (scoring joins
  // on it).
  athleteSeasonId: string;
  firstName: string;
  lastName: string;
  teamName: string;
  teamSlug: string;
  // Franchise.DisplayNameShort ("Texas"): mobile's compact labels. Optional
  // (null when the franchise row is missing); fall back to the full name.
  teamShortName?: string | null;
  position: 'QB' | 'RB' | 'WR' | 'TE' | 'K';
  opponentName: string | null;
  opponentSlug: string | null;
  opponentShortName?: string | null;
  // Opponent's relevant defensive allowance per game: net pass yds
  // allowed/G for QB/WR/TE, rush yds allowed/G for RB, points allowed/G
  // for K — aggregated server-side from what the opponent's opponents
  // actually gained; prior-season values until the opponent has
  // current-season games.
  opponentDefPerGame: number | null;
  // null before the athlete's first game of the season (week 1 everywhere)
  currentSeason: SeasonBlock | null;
  previousSeason: SeasonBlock | null;
  // The week's contest (null on bye) — slot lock anchoring.
  contestId?: string | null;
  contestStartUtc?: string | null;
};

type PickemAthletesResponse = { athletes: PickemAthlete[] };

export async function getAthletesByPosition(
  position: string,
  seasonYear: number,
  week: number,
  sport = 'football',
  league = 'ncaa',
  phase?: string,
): Promise<PickemAthletesResponse> {
  const response = await apiClient.get<PickemAthletesResponse>(
    `/api/${sport}/${league}/athletes/pickem?position=${encodeURIComponent(position)}&seasonYear=${seasonYear}&week=${week}${
      phase ? `&phase=${encodeURIComponent(phase)}` : ''
    }`,
  );
  return response.data;
}

// ── Roster persistence (server-side lineups, per-player derived locks) ──
// Mirrors sd-ui's PlayerPickemApi. The server resolves contest anchors and
// enforces the kickoff-5 lock rule itself; athlete payloads here are
// identity + display snapshot.

/** PlayerLineupSlotDto */
export type LineupSlot = {
  slotId: string;
  athleteId: string;
  athleteSeasonId: string;
  position: PickemAthlete['position'];
  firstName: string;
  lastName: string;
  teamName: string;
  teamSlug: string;
  contestId: string | null;
  contestStartUtc: string | null;
  opponentName: string | null;
  isLocked: boolean;
  points: number | null;
  statLine: string | null;
};

/** PlayerLineupDto */
export type Lineup = {
  leagueId: string;
  seasonYear: number;
  seasonWeek: number;
  slots: LineupSlot[];
  totalPoints: number;
};

const lineupPath = (leagueId: string, seasonYear: number, week: number) =>
  `/ui/leagues/${leagueId}/player-lineups/${seasonYear}/${week}/mine`;

export async function getMyLineup(
  leagueId: string,
  seasonYear: number,
  week: number,
): Promise<Lineup> {
  const response = await apiClient.get<Lineup>(lineupPath(leagueId, seasonYear, week));
  return response.data;
}

export async function upsertSlot(
  leagueId: string,
  seasonYear: number,
  week: number,
  slotId: string,
  athlete: PickemAthlete,
): Promise<LineupSlot> {
  const response = await apiClient.put<LineupSlot>(
    `${lineupPath(leagueId, seasonYear, week)}/slots/${encodeURIComponent(slotId)}`,
    {
      athleteId: athlete.athleteId,
      athleteSeasonId: athlete.athleteSeasonId,
      position: athlete.position,
      firstName: athlete.firstName,
      lastName: athlete.lastName,
      teamName: athlete.teamName,
      teamSlug: athlete.teamSlug,
      opponentName: athlete.opponentName ?? null,
    },
  );
  return response.data;
}

export async function clearSlot(
  leagueId: string,
  seasonYear: number,
  week: number,
  slotId: string,
): Promise<void> {
  await apiClient.delete(
    `${lineupPath(leagueId, seasonYear, week)}/slots/${encodeURIComponent(slotId)}`,
  );
}

// ── League leaderboard (Standings tab) ──

/** PlayerStandingsDto: cumulative lineup points with weekly winners. */
export type PlayerStandings = {
  leagueId: string;
  seasonYear: number;
  rows: {
    userId: string;
    displayName: string;
    totalPoints: number;
    weeklyWins: number;
    weeks: { week: number; points: number; isFinal: boolean; isWeeklyWinner: boolean }[];
  }[];
};

/** LeagueWeekLineupsDto: every member's lineup for one week. */
export type LeagueWeekLineups = {
  leagueId: string;
  seasonYear: number;
  seasonWeek: number;
  members: {
    userId: string;
    displayName: string;
    isSynthetic: boolean;
    totalPoints: number;
    // Another member's slots stay hidden until their game locks; this counts them.
    hiddenSlotCount: number;
    slots: LineupSlot[];
  }[];
};

export async function getStandings(leagueId: string, seasonYear: number): Promise<PlayerStandings> {
  const response = await apiClient.get<PlayerStandings>(
    `/ui/leagues/${leagueId}/player-lineups/${seasonYear}/standings`,
  );
  return response.data;
}

export async function getLeagueWeekLineups(
  leagueId: string,
  seasonYear: number,
  week: number,
): Promise<LeagueWeekLineups> {
  const response = await apiClient.get<LeagueWeekLineups>(
    `/ui/leagues/${leagueId}/player-lineups/${seasonYear}/${week}`,
  );
  return response.data;
}
