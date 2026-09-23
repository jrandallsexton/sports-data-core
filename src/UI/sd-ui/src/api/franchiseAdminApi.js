import apiClient from "./apiClient";

// Admin-only writes on the public franchises resource. These live on the
// standard slug-based API routes (not /ui, not /admin); the server gates
// them on the Admin role.
const FranchiseAdminApi = {
  // POST /api/{sport}/{league}/franchises/{slug}/seasons/{seasonYear}/enrich
  // 202 with { franchiseId, franchiseSeasonId, seasonYear, correlationId }.
  enrichFranchiseSeason: (sport, league, slug, seasonYear) =>
    apiClient.post(`/api/${sport}/${league}/franchises/${slug}/seasons/${seasonYear}/enrich`),
};

export default FranchiseAdminApi;
