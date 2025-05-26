// src/api/index.js
import Matchups from "./matchupsApi";
import Leaderboard from "./leaderboardApi";
import Auth from "./authApi";
import Users from "./usersApi";
import Venues from "./venuesApi";

const apiWrapper = {
  Matchups,
  Leaderboard,
  Auth,
  Users,
  Venues
};

export default apiWrapper;
