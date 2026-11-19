import apiClient from './apiClient';

const AnalyticsApi = {
  getFranchiseSeasonMetrics: (seasonYear) =>
    apiClient.get(`/ui/analytics/franchise-season/${seasonYear}`),

  // Season leaderboard for one athlete stat; category/stat are ESPN names
  // (e.g. passing / passingYards).
  getAthleteSeasonLeaders: (seasonYear, { category, stat, perGame = false, limit = 10 }) =>
    apiClient.get(`/ui/analytics/leaders/${seasonYear}`, {
      params: { category, stat, perGame, limit },
    }),

  // War Room Splits: a team's record over a season range, narrowed by
  // optional filters (omitted = any). Filter values are the Producer's enum
  // names: site Home/Away/Neutral, opponentRank Ranked/Top10/Unranked,
  // line Favorite/Underdog, kickoffWindow Noon/Afternoon/Night,
  // seasonType Regular/Postseason, spreadSize Close/Moderate/Large/Huge.
  // month is 1-12; opponent (franchise slug) and opponentConference come
  // from the response's options.
  getFranchiseSplits: (
    slug,
    {
      seasonFrom,
      seasonTo,
      site,
      opponentRank,
      line,
      kickoffWindow,
      conferenceGame,
      opponentConference,
      opponent,
      seasonType,
      month,
      spreadSize,
    },
  ) =>
    apiClient.get(`/ui/analytics/splits/${encodeURIComponent(slug)}`, {
      params: {
        seasonFrom,
        seasonTo,
        site,
        opponentRank,
        line,
        kickoffWindow,
        conferenceGame,
        opponentConference,
        opponent,
        seasonType,
        month,
        spreadSize,
      },
    }),
};

export default AnalyticsApi;
