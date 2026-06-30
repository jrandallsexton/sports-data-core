// src/api/leagues/leaguesApi.js

import apiClient from "../apiClient";

const BASE_PATH = "/ui/leagues";

/**
 * Creates a new NCAA football pick'em league.
 * @returns {Promise<{ id: string }>} Created league ID
 */
const createFootballNcaaLeague = async (request) => {
  const response = await apiClient.post(`${BASE_PATH}/football/ncaa`, request);
  return response.data;
};

/**
 * Creates a new NFL pick'em league.
 * @returns {Promise<{ id: string }>} Created league ID
 */
const createFootballNflLeague = async (request) => {
  const response = await apiClient.post(`${BASE_PATH}/football/nfl`, request);
  return response.data;
};

/**
 * Creates a new MLB pick'em league (admin-gated).
 * @returns {Promise<{ id: string }>} Created league ID
 */
const createBaseballMlbLeague = async (request) => {
  const response = await apiClient.post(`${BASE_PATH}/baseball/mlb`, request);
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
 * Searches registered users (by username or display name) who can be invited to
 * a league — excludes self, existing members, and synthetic users. No email is
 * returned.
 * @param {string} leagueId - League GUID
 * @param {string} q - Search term (min 2 chars on the BE)
 * @returns {Promise<{userId: string, username: string, displayName: string}[]>}
 */
const searchInviteableUsers = async (leagueId, q) => {
  const response = await apiClient.get(
    `${BASE_PATH}/${leagueId}/invite/search`,
    { params: { q } }
  );
  return response.data;
};

/**
 * Invites a registered user (picked from search) to a league. Triggers a push
 * notification; no email.
 * @param {string} leagueId - League GUID
 * @param {string} userId - Invitee's user GUID
 * @returns {Promise<void>}
 */
const inviteUser = async (leagueId, userId) => {
  await apiClient.post(`${BASE_PATH}/${leagueId}/invite/user`, { userId });
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

/**
 * Fetches weekly scores for all users in a league.
 * @param {string} leagueId - League GUID
 * @returns {Promise<LeagueScoresDto>} Weekly scores data
 */
const getLeagueScores = async (leagueId) => {
  const response = await apiClient.get(`${BASE_PATH}/${leagueId}/scores`);
  return response.data;
};

const LeaguesApi = {
  createFootballNcaaLeague,
  createFootballNflLeague,
  createBaseballMlbLeague,
  getLeagueById,
  getUserLeagues,
  joinLeague,
  deleteLeague,
  sendInvite,
  searchInviteableUsers,
  inviteUser,
  getPublicLeagues,
  getLeagueWeekOverview,
  getLeagueScores
};

export default LeaguesApi;
