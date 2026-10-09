import React from "react";
import { FaRobot } from "react-icons/fa";
import "./LeaderboardPage.css";

function WeeklyScoresTable({ scoresData, currentUserId }) {
  if (!scoresData) {
    return null;
  }

  const { weeks = [] } = scoresData;
  // Player Pick'em weeks carry lineup points and player counts instead of
  // correct-pick scores.
  const isPlayerLeague = scoresData.groupType === "PlayerPickem";

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
          isSynthetic: userScore.isSynthetic,
          weekScores: new Map()
        });
      }
      userMap.get(userScore.userId).weekScores.set(week.weekNumber, {
        score: userScore.score,
        pickCount: userScore.pickCount,
        points: userScore.points,
        playerCount: userScore.playerCount,
        isDropWeek: userScore.isDropWeek,
        isWeeklyWinner: userScore.isWeeklyWinner
      });
    });
  });

  // Convert to array and sort by userName
  const users = Array.from(userMap.values()).sort((a, b) => 
    a.userName.localeCompare(b.userName)
  );

  return (
    <div className="weekly-scores-container">
      <table className="leaderboard-table">
        <thead>
          <tr>
            <th>{isPlayerLeague ? "Member" : "Player"}</th>
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
                {user.isSynthetic && (
                  <FaRobot className="robot-icon" style={{ marginRight: "8px", color: "#61dafb" }} />
                )}
                {user.userName}
                {user.userId === currentUserId && (
                  <span className="you-label"> (You)</span>
                )}
              </td>
              {sortedWeeks.map(week => {
                const weekData = user.weekScores.get(week.weekNumber);
                const displayValue = isPlayerLeague
                  ? (weekData && weekData.playerCount > 0 && weekData.points != null
                      ? weekData.points.toFixed(1)
                      : '-')
                  : (weekData && weekData.pickCount > 0
                      ? weekData.score
                      : '-');
                const isWeeklyWinner = weekData && weekData.isWeeklyWinner;
                const isDropWeek = weekData && weekData.isDropWeek;
                
                return (
                  <td key={week.weekNumber}>
                    {isDropWeek ? (
                      <span style={{
                        textDecoration: 'line-through',
                        textDecorationColor: 'red',
                        textDecorationThickness: '3px',
                        color: '#888',
                        opacity: 0.6,
                        fontStyle: 'italic'
                      }}>
                        {displayValue}
                      </span>
                    ) : isWeeklyWinner ? (
                      <span style={{
                        border: '2px solid limegreen',
                        padding: '4px 8px',
                        borderRadius: '4px',
                        fontWeight: 'bold',
                        color: 'limegreen',
                        display: 'inline-block'
                      }}>
                        {displayValue}
                      </span>
                    ) : (
                      displayValue
                    )}
                  </td>
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
