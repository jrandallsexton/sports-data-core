import React from "react";
import "./LeaderboardPage.css";

/**
 * Player Pick'em season standings: members by cumulative lineup points,
 * with weekly wins. Data: PlayerPickemApi.getStandings (persisted weekly
 * totals the scoring consumers keep fresh). The team-league counterpart is
 * LeaderboardStandingsTable.
 */
function PlayerStandingsTable({ standings, currentUserId, loading }) {
  if (loading) {
    return <div className="loading">Loading standings...</div>;
  }

  const rows = standings?.rows ?? [];
  if (rows.length === 0) {
    return <p>No standings yet.</p>;
  }

  // Competition rank by total points: ties share a rank, the next skips.
  const rankOf = (row) => 1 + rows.filter((r) => r.totalPoints > row.totalPoints).length;

  return (
    <table className="leaderboard-table">
      <thead>
        <tr>
          <th>Rank</th>
          <th>Member</th>
          <th>Total Points</th>
          <th>Weekly Wins</th>
          <th>Weeks Played</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr
            key={row.userId}
            className={row.userId === currentUserId ? "current-user-row" : ""}
          >
            <td>{rankOf(row)}</td>
            <td>
              {row.displayName}
              {row.userId === currentUserId && <span className="you-label"> (You)</span>}
            </td>
            <td>{row.totalPoints.toFixed(1)}</td>
            <td>{row.weeklyWins > 0 ? `🏆 ${row.weeklyWins}` : "0"}</td>
            <td>{row.weeks?.length ?? 0}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default PlayerStandingsTable;
