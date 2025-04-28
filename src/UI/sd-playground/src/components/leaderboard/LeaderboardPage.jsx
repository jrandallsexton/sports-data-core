import { useState } from "react";
import leaderboard from "../../data/leaderboard";
import "./LeaderboardPage.css";
import { FaArrowDown, FaArrowUp } from "react-icons/fa";

function LeaderboardPage() {
  const [sortBy, setSortBy] = useState("totalPoints");
  const [sortOrder, setSortOrder] = useState("desc"); // 'asc' or 'desc'
  const currentUserId = 3; // Fake current user (Mike Brown)
  const currentWeek = 7; // You can mock this for now

  function handleSort(column) {
    if (sortBy === column) {
      // Same column clicked: toggle sort order
      setSortOrder((prev) => (prev === "asc" ? "desc" : "asc"));
    } else {
      // New column clicked: set column, default to descending
      setSortBy(column);
      setSortOrder("desc");
    }
  }

  const sortedLeaderboard = [...leaderboard].sort((a, b) => {
    if (sortOrder === "asc") {
      return a[sortBy] - b[sortBy];
    } else {
      return b[sortBy] - a[sortBy];
    }
  });

  const trueRanks = leaderboard
    .slice()
    .sort((a, b) => b.totalPoints - a.totalPoints)
    .map((player, index) => ({ id: player.id, rank: index + 1 }));

  return (
    <div>
      <h2>Group Leaderboard Through Week {currentWeek}</h2>
      <p>🏆 See how you stack up against your friends!</p>
      <table className="leaderboard-table">
        <thead>
          <tr>
            <th>Rank</th>
            <th>Player</th>
            <th className="sortable" onClick={() => handleSort("totalPoints")}>
              Total Points{" "}
              {sortBy === "totalPoints" &&
                (sortOrder === "asc" ? <FaArrowUp /> : <FaArrowDown />)}
            </th>
            <th className="sortable" onClick={() => handleSort("weeklyPoints")}>
              Weekly Points{" "}
              {sortBy === "weeklyPoints" &&
                (sortOrder === "asc" ? <FaArrowUp /> : <FaArrowDown />)}
            </th>
          </tr>
        </thead>
        <tbody>
          {sortedLeaderboard.map((user) => {
            const userTrueRank =
              trueRanks.find((u) => u.id === user.id)?.rank ?? "-";
            const movement = user.lastWeekRank
              ? user.lastWeekRank - userTrueRank
              : 0;

            return (
              <tr
                key={user.id}
                className={user.id === currentUserId ? "current-user-row" : ""}
              >
                <td className="rank-cell">
                  <div className="rank-number">{userTrueRank}</div>
                  {movement > 0 && (
                    <div className="movement-up">+{movement} 🔺</div>
                  )}
                  {movement < 0 && (
                    <div className="movement-down">{movement} 🔻</div>
                  )}
                  {movement === 0 && <div className="movement-same">➡️</div>}
                </td>

                <td>
                  {user.name}{" "}
                  {user.id === currentUserId && (
                    <span className="you-label">(You)</span>
                  )}
                </td>
                <td>{user.totalPoints}</td>
                <td>{user.weeklyPoints}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export default LeaderboardPage;
