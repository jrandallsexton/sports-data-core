import apiClient from "./apiClient";

const VenuesApi = {
  getAll: (sport, league) =>
    apiClient.get(`/api/${sport}/${league}/venues`),
  
  getBySlug: (sport, league, slug) =>
    apiClient.get(`/api/${sport}/${league}/venues/${slug}`)
};

export default VenuesApi;
