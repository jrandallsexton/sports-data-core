import apiClient from "./apiClient";

const RankingsApi = {
  // sport/league ride as query params — the season endpoint accepts them
  // (defaulting football/ncaa server-side). The week endpoints below are
  // not scope-aware yet (multi-sport TODO in
  // GetRankingsByPollWeekQueryHandler); RankingsPage gates non-NCAAFB
  // scopes until that lands.
  getSeasonRankings: (seasonYear, sport = "football", league = "ncaa") =>
    apiClient.get(`/ui/rankings/${seasonYear}?sport=${sport}&league=${league}`),
  getCurrentRankings: (seasonYear, seasonWeek) =>
    apiClient.get(`/ui/rankings/${seasonYear}/week/${seasonWeek}`),
  getCurrentPoll: (seasonYear, seasonWeek, pollName) =>
    apiClient.get(`/ui/rankings/${seasonYear}/week/${seasonWeek}/poll/${pollName}`),
  getRankingsByWeekId: (seasonWeekId, pollSlug) =>
    apiClient.get(`/ui/rankings/by-week/${seasonWeekId}/poll/${pollSlug}`),
};

export default RankingsApi;