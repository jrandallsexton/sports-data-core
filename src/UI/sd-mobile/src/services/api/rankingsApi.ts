import { apiClient } from './client';

// Matches SportsData.Core.Dtos.Canonical.RankingsByPollIdByWeekDto — the
// shape /ui/rankings serves (see sd-ui's RankingsWidget for the web twin).
export interface RankingsEntry {
  rank: number;
  franchiseName: string;
  franchiseSlug: string;
  franchiseSeasonId: string;
  franchiseLogoUrl?: string;
  franchiseLogoUrlDark?: string;
  franchiseLogoUrlLight?: string;
  wins: number;
  losses: number;
  points?: number;
  firstPlaceVotes?: number;
  previousRank?: number;
  trend?: string | null;
}

export interface RankingsPoll {
  pollId: string;
  pollName: string;
  seasonYear: number;
  week: number;
  pollDateUtc: string;
  hasPoints: boolean;
  hasFirstPlaceVotes: boolean;
  hasTrends: boolean;
  entries: RankingsEntry[];
}

export const rankingsKeys = {
  season: (sport: string, league: string, seasonYear: number) =>
    ['rankings', sport, league, seasonYear] as const,
  week: (sport: string, league: string, seasonYear: number, week: number) =>
    ['rankings', sport, league, seasonYear, 'week', week] as const,
};

export const rankingsApi = {
  // Latest published polls for a season. sport/league ride as query params
  // (the endpoint defaults football/ncaa server-side); the week endpoint is
  // not scope-aware yet — callers gate non-NCAAFB scopes, mirroring sd-ui's
  // RankingsPage.
  getSeasonRankings: (seasonYear: number, sport = 'football', league = 'ncaa') =>
    apiClient.get<RankingsPoll[]>(
      `/ui/rankings/${seasonYear}?sport=${sport}&league=${league}`
    ),

  // A specific week's polls (designated poll per week — see the backend's
  // poll_rank_asof docs for the date-based semantics).
  getWeekRankings: (seasonYear: number, week: number) =>
    apiClient.get<RankingsPoll[]>(`/ui/rankings/${seasonYear}/week/${week}`),
};
