import Matchups from "./matchupsApi";
import Leaderboard from "./leaderboardApi";
import Auth from "./authApi";
import Users from "./usersApi";
import Venues from "./venuesApi";
import TeamCard from "./teamCardApi";
import Picks from "./picksApi";

const apiWrapper = {
  Matchups,
  Leaderboard,
  Auth,
  Users,
  Venues,
  TeamCard,
  Picks
};

export default apiWrapper;
