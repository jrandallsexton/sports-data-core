import { apiClient } from './client';

// Matches SportsData.Core.Dtos.Canonical.SeasonPhaseDto.
// TypeCode: 1 = Preseason, 2 = Regular Season, 3 = Postseason, 4 = Off Season.
export interface SeasonPhase {
  typeCode: number;
  name: string;
  startDate: string;
  endDate: string;
}

// Matches SportsData.Core.Dtos.Canonical.CurrentSeasonDto.
export interface CurrentSeason {
  seasonYear: number;
  name: string;
  startDate: string;
  endDate: string;
  phases: SeasonPhase[];
}

export const REGULAR_SEASON_TYPE_CODE = 2;

export const seasonApi = {
  // GET /api/{sport}/{league}/seasons/current — current-or-upcoming season with
  // its phases. Raw phase data; the caller interprets it (e.g. the off-season
  // countdown reads the Regular Season phase's startDate). `sport`/`league` are
  // route segments, e.g. ('football','ncaa').
  getCurrentSeason: (sport: string, league: string) =>
    apiClient.get<CurrentSeason>(`/api/${sport}/${league}/seasons/current`),
};
