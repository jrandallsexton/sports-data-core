import Matchups from "./matchupsApi";
import Leaderboard from "./leaderboardApi";
import Auth from "./authApi";
import Users from "./usersApi";
import Venues from "./venuesApi";
import TeamCard from "./teamCardApi";
import Picks from "./picksApi";
import Previews from "./previewApi";
import Conferences from "./conferenceApi";
import Rankings  from "./rankingsApi";
import Contest from "./contestApi";
import Admin from "./adminApi";
import Analytics from "./analyticsApi";
import Maps from "./mapsApi";

const apiWrapper = {
  Matchups,
  Leaderboard,
  Auth,
  Users,
  Venues,
  TeamCard,
  Picks,
  Previews,
  Conferences,
  Rankings,
  Contest,
  Admin,
  Analytics,
  Maps
};

export default apiWrapper;
