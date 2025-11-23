// src/api/leagues/leaguesApi.js

import apiClient from "../apiClient";

const BASE_PATH = "/ui/leagues";

/**
 * Sends a request to create a new league.
 * @param {CreateLeagueRequest} request - DTO matching the backend model
 * @returns {Promise<{ id: string }>} Created league ID
 */
const createLeague = async (request) => {
  const response = await apiClient.post(BASE_PATH, request);
  return response.data;
};

/**
 * Fetches a league by ID.
 * @param {string} id - League GUID
 * @returns {Promise<LeagueDetailDto>} League details
 */
const getLeagueById = async (id) => {
  const response = await apiClient.get(`${BASE_PATH}/${id}`);
  return response.data;
};

/**
 * Fetches all leagues the current user belongs to.
 * @returns {Promise<LeagueSummaryDto[]>} Array of leagues
 */
const getUserLeagues = async () => {
  const response = await apiClient.get(BASE_PATH);
  return response.data;
};

/**
 * Joins a league by ID.
 * @param {string} id - League GUID
 * @returns {Promise<void>} No response body expected
 */
const joinLeague = async (id) => {
  await apiClient.post(`${BASE_PATH}/${id}/join`);
};

/**
 * Deletes a league by ID.
 * Only the league owner is authorized.
 * @param {string} id - League GUID
 * @returns {Promise<void>} No response body expected
 */
const deleteLeague = async (id) => {
  await apiClient.delete(`${BASE_PATH}/${id}`);
};

/**
 * Sends an email invitation to join a league.
 * @param {string} leagueId - League GUID
 * @param {string} email - Recipient's email address
 * @param {string} [inviteeName] - Optional recipient name
 * @returns {Promise<void>} No response body expected
 */
const sendInvite = async (leagueId, email, inviteeName = null) => {
  const requestBody = {
    leagueId,
    email,
    inviteeName,
  };
  await apiClient.post(`${BASE_PATH}/${leagueId}/invite`, requestBody);
};

/**
 * Fetches all public leagues the current user is not already a member of.
 * @returns {Promise<PublicLeagueDto[]>} Array of public leagues
 */
const getPublicLeagues = async () => {
  const response = await apiClient.get(`${BASE_PATH}/discover`);
  return response.data;
};

const getLeagueWeekOverview = async (leagueId, weekNumber) => {
  return apiClient.get(
    `${BASE_PATH}/${encodeURIComponent(leagueId)}/overview/${weekNumber}`
  );
};

const LeaguesApi = {
  createLeague,
  getLeagueById,
  getUserLeagues,
  joinLeague,
  deleteLeague,
  sendInvite,
  getPublicLeagues,
  getLeagueWeekOverview
};

export default LeaguesApi;
