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
      {leaderboardData && (
        <em>(as of week {leaderboardData.asOfWeek})</em>
      )}
      
      {leaderboardData && leaderboardData.items && leaderboardData.items.length > 0 ? (
        <div className="leaderboard-items">
          {leaderboardData.items.map((item) => (
            <div key={item.leagueId} className="leaderboard-item">
              <Link to="/app/leaderboard" className="leaderboard-link">
                {item.name} - {formatRankAsOrdinal(item.rank)}
              </Link>
            </div>
          ))}
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
