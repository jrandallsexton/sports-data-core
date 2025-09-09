import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import apiWrapper from '../../api/apiWrapper';
import './LeaderboardWidget.css';

const LeaderboardWidget = () => {
  const [leaderboardData, setLeaderboardData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Helper function to format rank as ordinal (1st, 2nd, 3rd, etc.)
  const formatRankAsOrdinal = (rank) => {
    const suffix = ['th', 'st', 'nd', 'rd'];
    const value = rank % 100;
    return rank + (suffix[(value - 20) % 10] || suffix[value] || suffix[0]);
  };

  // Calculate average rank across all leagues
  const calculateAverageRank = (items) => {
    if (!items || items.length === 0) return 0;
    const totalRank = items.reduce((sum, item) => sum + item.rank, 0);
    return totalRank / items.length;
  };

  useEffect(() => {
    const fetchLeaderboardWidget = async () => {
      try {
        setLoading(true);
        const response = await apiWrapper.Leaderboard.getWidgetForUser();
        setLeaderboardData(response.data);
        setError(null);
      } catch (err) {
        console.error('Error fetching leaderboard widget:', err);
        setError('Failed to load leaderboard data');
      } finally {
        setLoading(false);
      }
    };

    fetchLeaderboardWidget();
  }, []);

  if (loading) {
    return (
      <div className="card">
        <h2>Current Leaderboard</h2>
        <p>Loading...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="card">
        <h2>Your Ranking(s)</h2>
        <p className="error-text">{error}</p>
        <Link to="/app/leaderboard" className="card-link">
          View Full Leaderboard
        </Link>
      </div>
    );
  }

  return (
    <div className="card">
  <h2>Your Ranking(s)</h2>
      {/* {leaderboardData && (
        <em>(as of week {leaderboardData.asOfWeek})</em>
      )} */}
      
      {leaderboardData && leaderboardData.items && leaderboardData.items.length > 0 ? (
        <div className="league-records">
          <table className="pick-record-table">
            <thead>
              <tr>
                <th>League</th>
                <th>Rank</th>
              </tr>
            </thead>
            <tbody>
              {leaderboardData.items.map((item) => (
                <tr key={item.leagueId}>
                  <td>
                    <Link to="/app/leaderboard" className="leaderboard-link">
                      {item.name}
                    </Link>
                  </td>
                  <td>{formatRankAsOrdinal(item.rank)}</td>
                </tr>
              ))}
              
              {/* Average Rank Row */}
              {leaderboardData.items.length > 1 && (
                <tr className="overall-totals-row">
                  <td><strong>Average Rank</strong></td>
                  <td><strong>{formatRankAsOrdinal(Math.round(calculateAverageRank(leaderboardData.items)))}</strong></td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      ) : (
        <p>No leaderboard data available</p>
      )}
      
      <Link to="/app/leaderboard" className="card-link">
        View Full Leaderboard
      </Link>
    </div>
  );
};

export default LeaderboardWidget;
