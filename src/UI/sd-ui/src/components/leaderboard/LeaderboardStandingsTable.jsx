import React from "react";
import { FaArrowDown, FaArrowUp, FaRobot } from "react-icons/fa";
import "./LeaderboardPage.css";

function LeaderboardStandingsTable({ leaderboard, sortBy, sortOrder, handleSort, currentUserId, loading }) {
  const sortedLeaderboard = Array.isArray(leaderboard)
    ? [...leaderboard].sort((a, b) => {
        const valA = a[sortBy];
        const valB = b[sortBy];
        return sortOrder === "asc" ? valA - valB : valB - valA;
      })
    : [];

  if (loading) {
    return <div className="loading">Loading leaderboard...</div>;
  }

  return (
    <table className="leaderboard-table">
      <thead>
        <tr>
          <th>Rank</th>
          <th>Player</th>
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
            onClick={() => handleSort("totalPoints")}
          >
            Total Points{" "}
            {sortBy === "totalPoints" &&
              (sortOrder === "asc" ? <FaArrowUp /> : <FaArrowDown />)}
          </th>
          <th
            className="sortable"
            onClick={() => handleSort("totalPicks")}
          >
            Total Picks{" "}
            {sortBy === "totalPicks" &&
              (sortOrder === "asc" ? <FaArrowUp /> : <FaArrowDown />)}
          </th>
          <th
            className="sortable"
            onClick={() => handleSort("totalCorrect")}
          >
            Total Correct{" "}
            {sortBy === "totalCorrect" &&
              (sortOrder === "asc" ? <FaArrowUp /> : <FaArrowDown />)}
          </th>
          <th
            className="sortable"
            onClick={() => handleSort("pickAccuracy")}
          >
            Pick %{" "}
            {sortBy === "pickAccuracy" &&
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
                {user.name === "StatBot" && (
                  <FaRobot className="robot-icon" style={{ marginRight: "8px", color: "#61dafb" }} />
                )}
                {user.name}{" "}
                {user.userId === currentUserId && (
                  <span className="you-label">(You)</span>
                )}
              </td>
              <td>{user.currentWeekPoints}</td>
              <td>{user.totalPoints}</td>
              <td>{user.totalPicks}</td>
              <td>{user.totalCorrect}</td>
              <td>{user.pickAccuracy}%</td>
              <td>{user.weeklyAverage.toFixed(1)}</td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

export default LeaderboardStandingsTable;
