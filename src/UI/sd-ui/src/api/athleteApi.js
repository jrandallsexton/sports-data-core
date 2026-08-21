// src/api/athleteApi.js
import apiClient from "./apiClient";

const AthleteApi = {
  // GUID route — athlete slugs are not unique, unlike team slugs.
  getDetails: (sport, league, athleteId) =>
    apiClient.get(`/api/${sport}/${league}/athletes/${athleteId}`),
};

export default AthleteApi;
