// src/api/leaguesApi.js
import apiClient from "../apiClient";

/**
 * Sends a request to create a new league.
 * @param {CreateLeagueRequest} request - DTO matching the backend model
 * @returns {Promise<{ id: string }>} Created league ID
 */
const createLeague = async (request) => {
  const response = await apiClient.post("/ui/league", request);
  return response.data;
};

/**
 * Fetches a league by ID.
 * @param {string} id - League GUID
 * @returns {Promise<LeagueDetailDto>} League details
 */
const getLeagueById = async (id) => {
  const response = await apiClient.get(`/ui/league/${id}`);
  return response.data;
};

const LeaguesApi = {
  createLeague,
  getLeagueById,
};

export default LeaguesApi;
