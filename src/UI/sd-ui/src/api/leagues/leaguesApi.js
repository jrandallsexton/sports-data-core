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
 * Clones an existing league the user belongs to into a new one they own.
 * @param {string} leagueId - Source league GUID
 * @param {{ name: string, inviteMembers: boolean }} body
 * @returns {Promise<{ id: string }>} The new league's ID
 */
const cloneLeague = async (leagueId, body) => {
  const response = await apiClient.post(`${BASE_PATH}/${leagueId}/clone`, body);
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
 * @param {Object} [options]
 * @param {boolean} [options.includeDeactivated=false] - Also return past-season
 *   leagues. Those carry a non-null deactivatedUtc and are read-only.
 * @returns {Promise<LeagueSummaryDto[]>} Array of leagues
 */
const getUserLeagues = async ({ includeDeactivated = false } = {}) => {
  const response = await apiClient.get(BASE_PATH, {
    params: includeDeactivated ? { includeDeactivated: true } : undefined,
  });
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

/**
 * Fetches the sports currently gated from league creation, each with the UTC
 * instant it opens (e.g. NCAAFB waits for AP Poll release). Only active gates
 * are returned; a sport not listed is open.
 * @returns {Promise<{ gates: { sport: string, opensUtc: string }[] }>}
 */
/**
 * Fetches the season calendar for a sport — every week that can hold games
 * (Off Season excluded), StartDate-ordered, labeled with its phase where
 * numbering is ambiguous ("Week 4" vs "Preseason - Week 4"; week numbers
 * restart per phase). Drives the Week Range picker and drop-week limits.
 * @param {string} sport BE Sport enum name, e.g. "FootballNfl"
 * @returns {Promise<{ seasonYear: number, weeks: { id: string, number: number, label: string, phaseName: string, startDateUtc: string, endDateUtc: string }[] }>}
 */
const SPORT_ROUTE = {
  FootballNcaa: "football/ncaa",
  FootballNfl: "football/nfl",
  BaseballMlb: "baseball/mlb",
};

const getSeasonWeeks = async (sport) => {
  // Route follows the API's {sport}/{league}/{resource} convention (same as
  // game-dates), so the BE enum name translates to route slugs here.
  const route = SPORT_ROUTE[sport];
  if (!route) throw new Error(`Unknown sport: ${sport}`);
  const response = await apiClient.get(`${BASE_PATH}/${route}/season-weeks`);
  return response.data;
};

const getCreationAvailability = async () => {
  const response = await apiClient.get(`${BASE_PATH}/creation-availability`);
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
  cloneLeague,
  getLeagueById,
  getUserLeagues,
  joinLeague,
  deleteLeague,
  sendInvite,
  searchInviteableUsers,
  inviteUser,
  getPublicLeagues,
  getCreationAvailability,
  getSeasonWeeks,
  getLeagueWeekOverview,
  getLeagueScores
};

export default LeaguesApi;
