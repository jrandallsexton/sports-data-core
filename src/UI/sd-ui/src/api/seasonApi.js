import apiClient from "./apiClient";

// Session-lifetime promise cache for the current-season lookup. The
// answer changes roughly once a YEAR, yet multiple independent surfaces
// need it (off-season countdown fetches both sports, rankings resolve
// their season year) — without a cache the home page fired the same
// NCAA request twice per load. Caching the PROMISE (not the result)
// also dedupes concurrent first-load calls. A failed request is evicted
// so the next caller retries instead of caching an error forever.
const currentSeasonCache = new Map();

const SeasonApi = {
  getSeasonOverview: (seasonYear) =>
    apiClient.get(`/ui/season/${seasonYear}/overview`),

  // Current-or-upcoming season for a sport, with its phases. Standard endpoint
  // returning raw phase data (TypeCode + dates); the caller interprets it —
  // e.g. the off-season countdown reads the Regular Season (TypeCode 2) phase's
  // startDate. `sport`/`league` are the route segments, e.g. ("football","ncaa").
  getCurrentSeason: (sport, league) => {
    const key = `${sport}/${league}`;
    if (!currentSeasonCache.has(key)) {
      const request = apiClient
        .get(`/api/${sport}/${league}/seasons/current`)
        .catch((err) => {
          currentSeasonCache.delete(key);
          throw err;
        });
      currentSeasonCache.set(key, request);
    }
    return currentSeasonCache.get(key);
  },
};

export default SeasonApi;
