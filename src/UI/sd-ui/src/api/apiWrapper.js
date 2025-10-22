import Matchups from "./matchupsApi";
import Leaderboard from "./leaderboardApi";
import Auth from "./authApi";
import Users from "./usersApi";
import Venues from "./venuesApi";
import TeamCard from "./teamCardApi";
import Picks from "./picksApi";
import Conferences from "./conferenceApi";
import Rankings  from "./rankingsApi";
import Contest from "./contestApi";
import Admin from "./adminApi";

const apiWrapper = {
  Matchups,
  Leaderboard,
  Auth,
  Users,
  Venues,
  TeamCard,
  Picks,
  Conferences,
  Rankings,
  Contest,
  Admin,
};

export default apiWrapper;
