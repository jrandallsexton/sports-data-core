// src/api/index.js
import Matchups from "./matchupsApi";
import Leaderboard from "./leaderboardApi";
import Auth from "./authApi";
import Users from "./usersApi";

const apiWrapper = {
  Matchups,
  Leaderboard,
  Auth,
  Users
};

export default apiWrapper;
