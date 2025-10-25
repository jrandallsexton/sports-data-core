import React from "react";
import { FaRobot } from "react-icons/fa";
import "./LeaderboardPage.css";

function LeagueWeekOverviewTable({ overview }) {
  if (!overview || !overview.contests || !overview.userPicks) return null;
  const contests = overview.contests;
  const userPicks = overview.userPicks;

  // Get unique users
  const users = Array.from(
    new Map(userPicks.map(p => [p.userId, { userId: p.userId, user: p.user }])).values()
  );

  // Build a lookup: { [userId]: { [contestId]: pick } }
  const pickMap = {};
  userPicks.forEach(pick => {
    if (!pickMap[pick.userId]) pickMap[pick.userId] = {};
    pickMap[pick.userId][pick.contestId] = pick;
  });

  return (
    <div className="leaderboard-container">
      <table className="leaderboard-table">
        <thead>
          <tr>
            <th>Game</th>
            {users.map(user => (
              <th key={user.userId}>
                {user.user === "StatBot" && (
                  <FaRobot className="robot-icon" style={{ marginRight: "8px", color: "#61dafb" }} />
                )}
                {user.user}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {contests.map(contest => {
            const isLocked = !!contest.isLocked;
            if (!isLocked) return null;
            return (
              <tr key={contest.contestId}>
                <td>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ flex: 1, textAlign: 'left', color: contest.leagueWinnerFranchiseSeasonId === contest.awayFranchiseSeasonId ? 'limegreen' : undefined }}>
                      {contest.awayShort}
                    </span>
                    <span style={{ flex: 1, textAlign: 'center', color: '#888' }}>
                      {contest.homeSpread === null || contest.homeSpread === 0
                        ? 'Off'
                        : (typeof contest.homeSpread === 'number' && !isNaN(contest.homeSpread)
                            ? (contest.homeSpread > 0 ? '+' : '') + contest.homeSpread
                            : '')}
                    </span>
                    <span style={{ flex: 1, textAlign: 'right', color: contest.leagueWinnerFranchiseSeasonId === contest.homeFranchiseSeasonId ? 'limegreen' : undefined }}>
                      {contest.homeShort}
                    </span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'flex-end', fontSize: '0.9em', color: '#aaa' }}>
                    <span style={{ flex: 1 }}></span>
                    <span style={{ flex: 1, textAlign: 'center' }}></span>
                    <span style={{ flex: 1, textAlign: 'right' }}>
                      {typeof contest.awayScore === 'number' && typeof contest.homeScore === 'number'
                        ? `${contest.awayScore}-${contest.homeScore}`
                        : ''}
                    </span>
                  </div>
                </td>
                {users.map(user => {
                  const pick = pickMap[user.userId]?.[contest.contestId];
                  let teamShort = '';
                  if (pick) {
                    if (pick.franchiseId === contest.awayFranchiseSeasonId) {
                      teamShort = contest.awayShort;
                    } else if (pick.franchiseId === contest.homeFranchiseSeasonId) {
                      teamShort = contest.homeShort;
                    } else {
                      teamShort = pick.franchiseId; // fallback
                    }
                  }
                  return (
                    <td key={user.userId}>
                      {pick ? (
                        <span>
                          {teamShort}
                          {typeof pick.isCorrect === 'boolean' && (
                            <span style={{ marginLeft: 4, color: pick.isCorrect ? 'green' : 'red' }}>
                              {pick.isCorrect ? '✔' : '✘'}
                            </span>
                          )}
                        </span>
                      ) : ''}
                    </td>
                  );
                })}
              </tr>
            );
          })}
          {/* Point totals row */}
          <tr style={{ fontWeight: 'bold', background: '#222' }}>
            <td>Total</td>
            {users.map(user => {
              // Sum up correct picks for this user
              let total = 0;
              contests.forEach(contest => {
                const pick = pickMap[user.userId]?.[contest.contestId];
                if (pick && pick.isCorrect) total += 1;
              });
              return <td key={user.userId}>{total}</td>;
            })}
          </tr>
        </tbody>
      </table>
    </div>
  );
}

export default LeagueWeekOverviewTable;
