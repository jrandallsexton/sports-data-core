import apiClient from "./apiClient";

const VenuesApi = {
  getAll: (sport, league) =>
    apiClient.get(`/api/sports/${sport}/leagues/${league}/venues`),
  
  getBySlug: (sport, league, slug) =>
    apiClient.get(`/api/sports/${sport}/leagues/${league}/venues/${slug}`)
};

export default VenuesApi;
