import apiClient from "./apiClient";

const MapsApi = {
  getMap: (leagueId = null, week = null) => {
    const params = new URLSearchParams();
    if (leagueId) params.append('leagueId', leagueId);
    if (week) params.append('weekNumber', week);
    const queryString = params.toString();
    return apiClient.get(`/ui/map${queryString ? `?${queryString}` : ''}`);
  }
};

export default MapsApi;
