// src/api/teamCardApi.js
import apiClient from "./apiClient";

const TeamCardApi = {
  getBySlugAndSeason: (sport, league, slug, seasonYear) =>
    apiClient.get(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${slug}/${seasonYear}`
    ),
  // Finalized games only (FinalizedUtc IS NOT NULL). `asOfDate` (ISO 8601 from
  // LeagueWeekMatchupsDto.asOfDate, which equals the displayed week's
  // SeasonWeek.EndDate) is an inclusive upper bound on FinalizedUtc, so the
  // mini-schedule doesn't leak future-game results into a historical
  // pick-review context. Server returns newest-first.
  getFinalizedGames: (sport, league, slug, seasonYear, asOfDate) =>
    apiClient.get(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${slug}/${seasonYear}/finalized-games`,
      asOfDate ? { params: { asOfDate } } : undefined
    ),
  getStatistics: (sport, league, slug, seasonYear, franchiseSeasonId) =>
    apiClient.get(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${slug}/${seasonYear}/statistics`,
      {
        params: { franchiseSeasonId },
      }
    ),
  getMetrics: (sport, league, slug, seasonYear, franchiseSeasonId) =>
    apiClient.get(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${slug}/${seasonYear}/metrics`,
      {
        params: { franchiseSeasonId },
      }
    ),
  getRoster: (sport, league, slug, seasonYear) =>
    apiClient.get(
      `/api/${sport}/${league}/franchises/${slug}/seasons/${seasonYear}/roster`
    ),
};

export default TeamCardApi;
