import { useEffect, useState } from "react";
import { FaArrowDown, FaArrowUp, FaRobot } from "react-icons/fa";
import { useUserDto } from "../../contexts/UserContext";
import LeagueSelector from "../shared/LeagueSelector";
import apiWrapper from "../../api/apiWrapper";
import "./LeaderboardPage.css";

function LeaderboardPage() {
  const { userDto, loading: userLoading } = useUserDto();
  const leagues = Object.values(userDto?.leagues || []);
  const [selectedLeagueId, setSelectedLeagueId] = useState(null);
  const [leaderboard, setLeaderboard] = useState([]);
  const [sortBy, setSortBy] = useState("totalPoints");
  const [sortOrder, setSortOrder] = useState("desc");
  const [loading, setLoading] = useState(false);

  const currentUserId = userDto?.id ?? null;
  const currentWeek = 1; // 🔧 You can dynamically fetch this later

  useEffect(() => {
    if (leagues.length > 0 && !selectedLeagueId) {
      setSelectedLeagueId(leagues[0].id);
    }
  }, [leagues, selectedLeagueId]);

  useEffect(() => {
    const fetchLeaderboard = async () => {
      if (!selectedLeagueId) return;
      setLoading(true);
      try {
        const data = await apiWrapper.Leaderboard.getByGroupAndWeek(
          selectedLeagueId,
          currentWeek
        );

        console.log("Leaderboard API response:", data);
        console.log("Is array?", Array.isArray(data));
        console.log("Is data.data array?", Array.isArray(data.data));
        
        // Extract the actual leaderboard array from the response
        const leaderboardArray = data.data || data || [];
        
        // Ensure we always set an array
        setLeaderboard(Array.isArray(leaderboardArray) ? leaderboardArray : []);
      } catch (err) {
        console.error("Failed to load leaderboard", err);
        setLeaderboard([]);
      } finally {
        setLoading(false);
      }
    };

    fetchLeaderboard();
  }, [selectedLeagueId]);

  function handleSort(column) {
    if (sortBy === column) {
      setSortOrder((prev) => (prev === "asc" ? "desc" : "asc"));
    } else {
      setSortBy(column);
      setSortOrder("desc");
    }
  }

  const sortedLeaderboard = Array.isArray(leaderboard) 
    ? [...leaderboard].sort((a, b) => {
        const valA = a[sortBy];
        const valB = b[sortBy];
        return sortOrder === "asc" ? valA - valB : valB - valA;
      })
    : [];

  if (userLoading) {
    return <div className="leaderboard-container">Loading user data...</div>;
  }

  return (
    <div className="leaderboard-container">
      <h2>Group Leaderboard Through Week {currentWeek}</h2>
      <LeagueSelector
        leagues={leagues}
        selectedLeagueId={selectedLeagueId}
        setSelectedLeagueId={setSelectedLeagueId}
      />
      <p>🏆 See how you stack up against your friends!</p>

      {loading ? (
        <div className="loading">Loading leaderboard...</div>
      ) : (
        <table className="leaderboard-table">
          <thead>
            <tr>
              <th>Rank</th>
              <th>Player</th>
              <th
                className="sortable"
                onClick={() => handleSort("totalPoints")}
              >
                Total Points{" "}
                {sortBy === "totalPoints" &&
                  (sortOrder === "asc" ? <FaArrowUp /> : <FaArrowDown />)}
              </th>
              <th
                className="sortable"
                onClick={() => handleSort("currentWeekPoints")}
              >
                Current Week{" "}
                {sortBy === "currentWeekPoints" &&
                  (sortOrder === "asc" ? <FaArrowUp /> : <FaArrowDown />)}
              </th>
              <th
                className="sortable"
                onClick={() => handleSort("weeklyAverage")}
              >
                Weekly Avg{" "}
                {sortBy === "weeklyAverage" &&
                  (sortOrder === "asc" ? <FaArrowUp /> : <FaArrowDown />)}
              </th>
            </tr>
          </thead>
          <tbody>
            {sortedLeaderboard.map((user) => {
              const movement = user.lastWeekRank
                ? user.lastWeekRank - user.rank
                : null;

              return (
                <tr
                  key={user.userId}
                  className={
                    user.userId === currentUserId ? "current-user-row" : ""
                  }
                >
                  <td className="rank-cell">
                    <div className="rank-number">{user.rank}</div>
                    {movement !== null && movement > 0 && (
                      <div className="movement-up">+{movement} 🔺</div>
                    )}
                    {movement !== null && movement < 0 && (
                      <div className="movement-down">{movement} 🔻</div>
                    )}
                    {movement !== null && movement === 0 && (
                      <div className="movement-same">➡️</div>
                    )}
                  </td>

                  <td>
                    {user.name === "sportDeets" && (
                      <FaRobot className="robot-icon" style={{ marginRight: "8px", color: "#61dafb" }} />
                    )}
                    {user.name}{" "}
                    {user.userId === currentUserId && (
                      <span className="you-label">(You)</span>
                    )}
                  </td>
                  <td>{user.totalPoints}</td>
                  <td>{user.currentWeekPoints}</td>
                  <td>{user.weeklyAverage.toFixed(1)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </div>
  );
}

export default LeaderboardPage;
