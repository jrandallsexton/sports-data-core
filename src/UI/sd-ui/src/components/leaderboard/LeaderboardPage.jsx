import { useEffect, useState } from "react";
import { useUserDto } from "../../contexts/UserContext";
import { useLeagueContext } from "../../contexts/LeagueContext";
import LeagueSelector from "../shared/LeagueSelector";
import apiWrapper from "../../api/apiWrapper";
import LeaguesApi from '../../api/leagues/leaguesApi';
import LeaderboardStandingsTable from "./LeaderboardStandingsTable";
import LeagueWeekOverviewTable from "./LeagueWeekOverviewTable";
import WeeklyScoresTable from "./WeeklyScoresTable";
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
  const [selectedWeek, setSelectedWeek] = useState(null); // Start with null instead of 1
  const [weeklyScores, setWeeklyScores] = useState(null);
  const [activeTab, setActiveTab] = useState("standings");
  const [showBots, setShowBots] = useState(true);

  const currentUserId = userDto?.id ?? null;

  useEffect(() => {
    if (!userLoading && leagues.length > 0) {
      initializeLeagueSelection(leagues);
    }
  }, [userLoading, leagues, initializeLeagueSelection]);

  useEffect(() => {
    const fetchLeaderboard = async () => {
      if (!selectedLeagueId) return;
      setLoading(true);
      try {
        const data = await apiWrapper.Leaderboard.getByGroupAndWeek(
          selectedLeagueId
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
  }, [selectedLeagueId]);

  // Find the selected league and its maxSeasonWeek
  const selectedLeague = leagues.find(l => l.id === selectedLeagueId);
  const maxSeasonWeek = selectedLeague?.maxSeasonWeek || 1;

  // Set selectedWeek to maxSeasonWeek when league changes
  useEffect(() => {
    if (selectedLeagueId && maxSeasonWeek) {
      setSelectedWeek(maxSeasonWeek);
    }
  }, [selectedLeagueId, maxSeasonWeek]);

  // Add the API call for week overview using the selectedLeagueId from context
  // Only run when selectedWeek is properly set (not null)
  useEffect(() => {
    if (selectedLeagueId && selectedWeek !== null) {
      LeaguesApi.getLeagueWeekOverview(selectedLeagueId, selectedWeek)
        .then(response => {
          setOverview(response.data);
        })
        .catch(err => {
          // Optionally log error
        });
    }
  }, [selectedLeagueId, selectedWeek]);

  // Fetch weekly scores when league changes
  useEffect(() => {
    const fetchWeeklyScores = async () => {
      if (!selectedLeagueId) {
        setWeeklyScores(null);
        return;
      }
      try {
        const data = await LeaguesApi.getLeagueScores(selectedLeagueId);
        setWeeklyScores(data);
      } catch (err) {
        console.error("Failed to load weekly scores", err);
        setWeeklyScores(null);
      }
    };
    fetchWeeklyScores();
  }, [selectedLeagueId]);

  // Generate week options based on maxSeasonWeek
  const weekOptions = Array.from({ length: maxSeasonWeek }, (_, i) => i + 1);

  // Filter functions for synthetic users
  const filterLeaderboard = (data) => {
    if (showBots) return data;
    return data.filter(user => !user.isSynthetic);
  };

  const filterWeeklyScores = (data) => {
    if (!data || showBots) return data;
    const filteredWeeks = data.weeks.map(week => ({
      ...week,
      userScores: week.userScores.filter(user => !user.isSynthetic)
    }));
    return { ...data, weeks: filteredWeeks };
  };

  const filterOverview = (data) => {
    if (!data || showBots) return data;
    const filteredUserPicks = data.userPicks.filter(pick => !pick.isSynthetic);
    return { ...data, userPicks: filteredUserPicks };
  };

  function handleSort(column) {
    if (sortBy === column) {
      setSortOrder((prev) => (prev === "asc" ? "desc" : "asc"));
    } else {
      setSortBy(column);
      setSortOrder("desc");
    }
  }

  if (userLoading) {
    return <div className="leaderboard-container">Loading user data...</div>;
  }

  return (
    <div className="leaderboard-container">
      <div style={{ display: 'flex', alignItems: 'center', gap: '24px' }}>
        <LeagueSelector
          leagues={leagues}
          selectedLeagueId={selectedLeagueId}
          setSelectedLeagueId={setSelectedLeagueId}
        />
        
        {/* Show Bots Filter */}
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={showBots}
            onChange={(e) => setShowBots(e.target.checked)}
          />
          <span>Show Bots</span>
        </label>
      </div>

      {/* Tab Navigation */}
      <div className="tab-navigation">
        <button
          className={`tab-button ${activeTab === "standings" ? "active" : ""}`}
          onClick={() => setActiveTab("standings")}
        >
          Standings
        </button>
        <button
          className={`tab-button ${activeTab === "allWeeks" ? "active" : ""}`}
          onClick={() => setActiveTab("allWeeks")}
        >
          All Weeks
        </button>
        <button
          className={`tab-button ${activeTab === "byWeek" ? "active" : ""}`}
          onClick={() => setActiveTab("byWeek")}
        >
          By Week
        </button>
      </div>

      {/* Standings Tab */}
      {activeTab === "standings" && (
        <>
          {loading ? (
            <div className="loading">Loading leaderboard...</div>
          ) : (
            <LeaderboardStandingsTable
              leaderboard={filterLeaderboard(leaderboard)}
              sortBy={sortBy}
              sortOrder={sortOrder}
              handleSort={handleSort}
              currentUserId={currentUserId}
              loading={loading}
            />
          )}
        </>
      )}

      {/* All Weeks Tab */}
      {activeTab === "allWeeks" && (
        <WeeklyScoresTable
          scoresData={filterWeeklyScores(weeklyScores)}
          currentUserId={currentUserId}
        />
      )}

      {/* By Week Tab */}
      {activeTab === "byWeek" && (
        <>
          {selectedWeek !== null && (
            <div className="league-selector" style={{ margin: '16px 0' }}>
              <label htmlFor="week-select" style={{ marginRight: 8, fontWeight: 500, color: '#ccc', fontSize: '1rem', lineHeight: '32px' }}>Week:</label>
              <select
                id="week-select"
                value={selectedWeek}
                onChange={e => setSelectedWeek(Number(e.target.value))}
                className="week-selector-select"
              >
                {weekOptions.map(week => (
                  <option key={week} value={week}>Week {week}</option>
                ))}
              </select>
            </div>
          )}

          <LeagueWeekOverviewTable
            overview={filterOverview(overview)}
          />
        </>
      )}
    </div>
  );
}

export default LeaderboardPage;
