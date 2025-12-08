import React from "react";
import "./LeaderboardPage.css";

function WeeklyScoresTable({ scoresData, currentUserId }) {
  if (!scoresData) {
    return null;
  }

  const { leagueName, weeks = [] } = scoresData;

  if (weeks.length === 0) {
    return (
      <div className="weekly-scores-container">
        <h2>Weekly Scores</h2>
        <p>No weekly scores available.</p>
      </div>
    );
  }

  // Sort weeks by weekNumber in ascending order
  const sortedWeeks = [...weeks].sort((a, b) => a.weekNumber - b.weekNumber);

  // Build a map of all unique users
  const userMap = new Map();
  sortedWeeks.forEach(week => {
    week.userScores.forEach(userScore => {
      if (!userMap.has(userScore.userId)) {
        userMap.set(userScore.userId, {
          userId: userScore.userId,
          userName: userScore.userName,
          weekScores: new Map()
        });
      }
      userMap.get(userScore.userId).weekScores.set(week.weekNumber, {
        score: userScore.score,
        pickCount: userScore.pickCount
      });
    });
  });

  // Convert to array and sort by userName
  const users = Array.from(userMap.values()).sort((a, b) => 
    a.userName.localeCompare(b.userName)
  );

  return (
    <div className="weekly-scores-container" style={{ marginTop: '32px' }}>
      <h2>Weekly Scores - {leagueName}</h2>
      <table className="leaderboard-table">
        <thead>
          <tr>
            <th>Player</th>
            {sortedWeeks.map(week => (
              <th key={week.weekNumber}>Week {week.weekNumber}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {users.map(user => (
            <tr
              key={user.userId}
              className={user.userId === currentUserId ? "current-user-row" : ""}
            >
              <td>
                {user.userName}
                {user.userId === currentUserId && (
                  <span className="you-label"> (You)</span>
                )}
              </td>
              {sortedWeeks.map(week => {
                const weekData = user.weekScores.get(week.weekNumber);
                const displayValue = weekData && weekData.pickCount > 0 
                  ? weekData.score 
                  : '-';
                return (
                  <td key={week.weekNumber}>{displayValue}</td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default WeeklyScoresTable;
