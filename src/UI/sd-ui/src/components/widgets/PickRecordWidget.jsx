import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import apiWrapper from '../../api/apiWrapper';
import './PickRecordWidget.css';

const PickRecordWidget = () => {
  const [pickRecordData, setPickRecordData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchPickRecordWidget = async () => {
      try {
        setLoading(true);
        const response = await apiWrapper.Picks.getWidgetForUser();
        setPickRecordData(response.data);
        setError(null);
      } catch (err) {
        console.error('Error fetching pick record widget:', err);
        setError('Failed to load pick record data');
      } finally {
        setLoading(false);
      }
    };

    fetchPickRecordWidget();
  }, []);

  // Calculate totals across all leagues
  const calculateTotals = (items) => {
    if (!items || items.length === 0) return { correct: 0, incorrect: 0, pushes: 0, accuracy: 0 };
    
    const totals = items.reduce((acc, item) => {
      acc.correct += item.correct;
      acc.incorrect += item.incorrect;
      acc.pushes += item.pushes || 0;
      return acc;
    }, { correct: 0, incorrect: 0, pushes: 0 });

    const totalPicks = totals.correct + totals.incorrect;
    totals.accuracy = totalPicks > 0 ? (totals.correct / totalPicks) * 100 : 0;
    
    return totals;
  };

  if (loading) {
    return (
      <div className="card">
        <h2>Your Pick Record</h2>
        <p>Loading...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="card">
        <h2>Your Pick Record</h2>
        <p className="error-text">{error}</p>
        <Link to="/app/your-picks" className="card-link">
          View Your Weekly Picks
        </Link>
      </div>
    );
  }

  const totals = pickRecordData ? calculateTotals(pickRecordData.items) : { correct: 0, incorrect: 0, pushes: 0, accuracy: 0 };

  return (
    <div className="card">
      <h2>Your Pick Record</h2>
      {pickRecordData && (
        <em>(as of week {pickRecordData.asOfWeek})</em>
      )}
      
      {/* Individual League Records */}
      {pickRecordData && pickRecordData.items && pickRecordData.items.length > 0 ? (
        <div className="league-records">
          <table className="pick-record-table">
            <thead>
              <tr>
                <th>League</th>
                <th>W</th>
                <th>L</th>
                <th>%</th>
              </tr>
            </thead>
            <tbody>
              {pickRecordData.items.map((item) => (
                <tr key={item.leagueId}>
                  <td>
                    <Link to="/app/leaderboard" className="league-link">
                      {item.leagueName}
                    </Link>
                  </td>
                  <td>{item.correct}</td>
                  <td>{item.incorrect}</td>
                  <td>{(item.accuracy * 100).toFixed(1)}%</td>
                </tr>
              ))}
              
              {/* Overall Totals Row */}
              {pickRecordData.items.length > 1 && (
                <tr className="overall-totals-row">
                  <td><strong>Overall Totals</strong></td>
                  <td><strong>{totals.correct}</strong></td>
                  <td><strong>{totals.incorrect}</strong></td>
                  <td><strong>{totals.accuracy.toFixed(1)}%</strong></td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      ) : (
        <p>No pick record data available</p>
      )}
      
      <Link to="/app/your-picks" className="card-link">
        View Your Weekly Picks
      </Link>
    </div>
  );
};

export default PickRecordWidget;
