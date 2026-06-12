// Uses publicApiClient (no auth interceptors) because the Results
// retrospective is intentionally exposed at /results/... without login.
// See api/publicApiClient.js for the rationale.
import publicApiClient from "./publicApiClient";

const ResultsApi = {
  getSeasonResults: (sport, league, seasonYear) =>
    publicApiClient.get(`/ui/results/sport/${sport}/league/${league}/${seasonYear}`),
};

export default ResultsApi;
