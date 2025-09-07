import { useEffect, useState } from "react";
import { FaArrowDown, FaArrowUp, FaRobot } from "react-icons/fa";
import { useUserDto } from "../../contexts/UserContext";
import { useLeagueContext } from "../../contexts/LeagueContext";
import LeagueSelector from "../shared/LeagueSelector";
import apiWrapper from "../../api/apiWrapper";
import LeaguesApi from '../../api/leagues/leaguesApi';
import "./LeaderboardPage.css";

function LeaderboardPage() {
  const { userDto, loading: userLoading } = useUserDto();
  const { selectedLeagueId, setSelectedLeagueId, initializeLeagueSelection } = useLeagueContext();
  const leagues = Object.values(userDto?.leagues || []);

  const [leaderboard, setLeaderboard] = useState([]);
  const [sortBy, setSortBy] = useState("totalPoints");
  const [sortOrder, setSortOrder] = useState("desc");
  const [loading, setLoading] = useState(false);
  const [overview, setOverview] = useState(null);
  const [selectedWeek, setSelectedWeek] = useState(1);

  const currentUserId = userDto?.id ?? null;

  useEffect(() => {
    if (!userLoading && leagues.length > 0) {
      initializeLeagueSelection(leagues);
    }
  }, [userLoading, leagues, initializeLeagueSelection]);

  useEffect(() => {
    const fetchLeaderboard = async () => {
      if (!selectedLeagueId || !selectedWeek) return;
      setLoading(true);
      try {
        const data = await apiWrapper.Leaderboard.getByGroupAndWeek(
          selectedLeagueId,
          selectedWeek
        );
        // Extract the actual leaderboard array from the response
        const leaderboardArray = data.data || data || [];
        setLeaderboard(Array.isArray(leaderboardArray) ? leaderboardArray : []);
      } catch (err) {
        console.error("Failed to load leaderboard", err);
        setLeaderboard([]);
      } finally {
        setLoading(false);
      }
    };
    fetchLeaderboard();
  }, [selectedLeagueId, selectedWeek]);

  // Add the API call for week overview using the selectedLeagueId from context
  useEffect(() => {
    if (selectedLeagueId && selectedWeek) {
      LeaguesApi.getLeagueWeekOverview(selectedLeagueId, selectedWeek)
        .then(response => {
          setOverview(response.data);
        })
        .catch(err => {
          // Optionally log error
        });
    }
  }, [selectedLeagueId, selectedWeek]);

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

  // Render the grid if overview data is available (users as columns, contests as rows)
  const renderLeagueWeekOverviewTable = () => {
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
                <th key={user.userId}>{user.user}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {contests.map(contest => {
              const isLocked = !!contest.finalizedUtc;
              if (!isLocked) return null;
              return (
                <tr key={contest.contestId}>
                  <td>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ flex: 1, textAlign: 'left' }}>
                        {contest.awayShort} @ {contest.homeShort}
                      </span>
                      <span style={{ flex: 1, textAlign: 'center', color: '#888' }}>
                        {typeof contest.homeSpread === 'number' && !isNaN(contest.homeSpread)
                          ? (contest.homeSpread > 0 ? '+' : '') + contest.homeSpread
                          : ''}
                      </span>
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
  };

  if (userLoading) {
    return <div className="leaderboard-container">Loading user data...</div>;
  }

  // For now, assume 18 weeks. You can make this dynamic if needed.
  const weekOptions = Array.from({ length: 18 }, (_, i) => i + 1);

  return (
    <div className="leaderboard-container">
      <LeagueSelector
        leagues={leagues}
        selectedLeagueId={selectedLeagueId}
        setSelectedLeagueId={setSelectedLeagueId}
      />

      <div style={{ margin: '16px 0' }}>
        <label htmlFor="week-select" style={{ marginRight: 8, fontWeight: 500 }}>Week:</label>
        <select
          id="week-select"
          value={selectedWeek}
          onChange={e => setSelectedWeek(Number(e.target.value))}
          style={{ padding: '4px 8px', borderRadius: 4 }}
        >
          {weekOptions.map(week => (
            <option key={week} value={week}>Week {week}</option>
          ))}
        </select>
      </div>

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
                    {user.name === "StatBot" && (
                      <FaRobot className="robot-icon" style={{ marginRight: "8px", color: "#61dafb" }} />
                    )}
                    {user.name}{" "}
                    {user.userId === currentUserId && (
                      <span className="you-label">(You)</span>
                    )}
                  </td>
                  <td>{user.totalPoints}</td>
                  <td>{user.totalPicks}</td>
                  <td>{user.totalCorrect}</td>
                  <td>{user.pickAccuracy}%</td>
                  <td>{user.currentWeekPoints}</td>
                  <td>{user.weeklyAverage.toFixed(1)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}

      {renderLeagueWeekOverviewTable()}
    </div>
  );
}

export default LeaderboardPage;
