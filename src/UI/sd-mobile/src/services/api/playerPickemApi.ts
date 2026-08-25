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
  firstName: string;
  lastName: string;
  teamName: string;
  teamSlug: string;
  position: 'QB' | 'RB' | 'WR' | 'TE' | 'K';
  opponentName: string | null;
  opponentSlug: string | null;
  // Opponent's relevant defensive allowance per game: net pass yds
  // allowed/G for QB/WR/TE, rush yds allowed/G for RB, points allowed/G
  // for K — aggregated server-side from what the opponent's opponents
  // actually gained; prior-season values until the opponent has
  // current-season games.
  opponentDefPerGame: number | null;
  // null before the athlete's first game of the season (week 1 everywhere)
  currentSeason: SeasonBlock | null;
  previousSeason: SeasonBlock | null;
};

type PickemAthletesResponse = { athletes: PickemAthlete[] };

export async function getAthletesByPosition(
  position: string,
  seasonYear: number,
  week: number,
  sport = 'football',
  league = 'ncaa',
): Promise<PickemAthletesResponse> {
  const response = await apiClient.get<PickemAthletesResponse>(
    `/api/${sport}/${league}/athletes/pickem?position=${encodeURIComponent(position)}&seasonYear=${seasonYear}&week=${week}`,
  );
  return response.data;
}
