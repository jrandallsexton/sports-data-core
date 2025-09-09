import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import apiWrapper from '../../api/apiWrapper';
import './AiRecordWidget.css';

const AiRecordWidget = () => {
  const [aiRecordData, setAiRecordData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchAiRecordWidget = async () => {
      try {
        setLoading(true);
        const response = await apiWrapper.Picks.getWidgetForSynthetic();
        setAiRecordData(response.data);
        setError(null);
      } catch (err) {
        console.error('Error fetching AI record widget:', err);
        setError('Failed to load AI accuracy data');
      } finally {
        setLoading(false);
      }
    };

    fetchAiRecordWidget();
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
        <h2>sportDeets AI Accuracy</h2>
        <p>Loading...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="card">
        <h2>sportDeets AI Accuracy</h2>
        <p className="error-text">{error}</p>
        <Link to="/app/ai-performance" className="card-link">
          See Full AI Stats
        </Link>
      </div>
    );
  }

  const totals = aiRecordData ? calculateTotals(aiRecordData.items) : { correct: 0, incorrect: 0, pushes: 0, accuracy: 0 };

  return (
    <div className="card">
  <h2>sportDeets AI Accuracy</h2>
      {/* {aiRecordData && (
        <em>(as of week {aiRecordData.asOfWeek})</em>
      )} */}
      
      {aiRecordData && aiRecordData.items && aiRecordData.items.length > 0 ? (
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
              {aiRecordData.items.map((item) => (
                <tr key={item.leagueId}>
                  <td>
                    <Link to="/app/ai-performance" className="league-link">
                      {item.leagueName}
                    </Link>
                  </td>
                  <td>{item.correct}</td>
                  <td>{item.incorrect}</td>
                  <td>{(item.accuracy * 100).toFixed(1)}%</td>
                </tr>
              ))}
              
              {/* Overall Totals Row */}
              {aiRecordData.items.length > 1 && (
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
        <p>No AI accuracy data available</p>
      )}
      
      <Link to="/app/ai-performance" className="card-link">
        See Full AI Stats
      </Link>
    </div>
  );
};

export default AiRecordWidget;
