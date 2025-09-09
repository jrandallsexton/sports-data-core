import React, { useEffect, useState } from "react";
import apiWrapper from "../../api/apiWrapper";
import "./RankingsWidget.css";

function RankingsWidget() {
  const [rankings, setRankings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    async function fetchRankings() {
      setLoading(true);
      setError(null);
      try {
        const data = await apiWrapper.Rankings.getCurrentRankings(2025, 3);
        setRankings(data?.data || []);
      } catch (err) {
        setError("Failed to load rankings");
      } finally {
        setLoading(false);
      }
    }
    fetchRankings();
  }, []);

  return (
    <div className="rankings-widget">
      <h2>AP Top 25 - Week 3 (2025)</h2>
      {loading ? (
        <div>Loading rankings...</div>
      ) : error ? (
        <div className="error">{error}</div>
      ) : (
        <table className="rankings-table">
          <thead>
            <tr>
              <th>Rank</th>
              <th>Team</th>
              <th>Record</th>
            </tr>
          </thead>
          <tbody>
            {rankings.map((team, idx) => (
              <tr key={team.franchiseId || idx}>
                <td>{team.rank}</td>
                <td>{team.school}</td>
                <td>{team.record}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

export default RankingsWidget;
