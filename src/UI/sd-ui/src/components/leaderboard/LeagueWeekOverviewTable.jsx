import React from "react";
import { FaRobot } from "react-icons/fa";
import "./LeaderboardPage.css";

function LeagueWeekOverviewTable({ overview }) {
  if (!overview || !overview.contests || !overview.userPicks) return null;
  const contests = overview.contests;
  const userPicks = overview.userPicks;

  // Get unique users
  const users = Array.from(
    new Map(userPicks.map(p => [p.userId, { userId: p.userId, user: p.user, isSynthetic: p.isSynthetic }])).values()
  );

  // Build a lookup: { [userId]: { [contestId]: pick } }
  const pickMap = {};
  userPicks.forEach(pick => {
    if (!pickMap[pick.userId]) pickMap[pick.userId] = {};
    pickMap[pick.userId][pick.contestId] = pick;
  });

  return (
    <table className="leaderboard-table">
      <thead>
        <tr>
          <th>Game</th>
          {users.map(user => (
            <th key={user.userId}>
              {(user.user === "StatBot" || user.isSynthetic) && (
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
                    <span style={{ flex: 1, textAlign: 'left' }}>
                      <a 
                        href={`/app/sport/football/ncaa/team/${contest.awaySlug}/2025`}
                        target="_blank"
                        rel="noopener noreferrer"
                        style={{ 
                          color: contest.leagueWinnerFranchiseSeasonId === contest.awayFranchiseSeasonId ? 'limegreen' : '#61dafb',
                          textDecoration: 'none'
                        }}
                      >
                        {contest.awayShort}
                      </a>
                    </span>
                    <span style={{ flex: 1, textAlign: 'center', color: '#888' }}>
                      {contest.homeSpread === null || contest.homeSpread === 0
                        ? 'Off'
                        : (typeof contest.homeSpread === 'number' && !isNaN(contest.homeSpread)
                            ? (contest.homeSpread > 0 ? '+' : '') + contest.homeSpread
                            : '')}
                    </span>
                    <span style={{ flex: 1, textAlign: 'right' }}>
                      <a 
                        href={`/app/sport/football/ncaa/team/${contest.homeSlug}/2025`}
                        target="_blank"
                        rel="noopener noreferrer"
                        style={{ 
                          color: contest.leagueWinnerFranchiseSeasonId === contest.homeFranchiseSeasonId ? 'limegreen' : '#61dafb',
                          textDecoration: 'none'
                        }}
                      >
                        {contest.homeShort}
                      </a>
                    </span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'flex-end', fontSize: '0.9em', color: '#aaa' }}>
                    <span style={{ flex: 1 }}></span>
                    <span style={{ flex: 1, textAlign: 'center' }}></span>
                    <span style={{ flex: 1, textAlign: 'right' }}>
                      {typeof contest.awayScore === 'number' && typeof contest.homeScore === 'number' ? (
                        <a 
                          href={`/app/sport/football/ncaa/contest/${contest.contestId}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          style={{ color: '#aaa', textDecoration: 'none' }}
                        >
                          {`${contest.awayScore}-${contest.homeScore}`}
                        </a>
                      ) : ''}
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
                          {pick.confidencePoints && (
                            <span className="confidence-badge">
                              {pick.confidencePoints}
                            </span>
                          )}
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
                if (pick) {
                  if (pick.pointsAwarded !== undefined && pick.pointsAwarded !== null) {
                    total += pick.pointsAwarded;
                  } else if (pick.isCorrect) {
                    total += (pick.confidencePoints || 1);
                  }
                }
              });
              return <td key={user.userId}>{total}</td>;
            })}
          </tr>
        </tbody>
      </table>
  );
}

export default LeagueWeekOverviewTable;
