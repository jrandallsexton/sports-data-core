import React, { useState, useEffect } from 'react';
import apiWrapper from '../../api/apiWrapper';
import './FranchiseMetricsGrid.css';

function FranchiseMetricsGrid() {
  const [metrics, setMetrics] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [sortBy, setSortBy] = useState('franchiseName');
  const [sortOrder, setSortOrder] = useState('asc');
  const [showAll, setShowAll] = useState(false);

  useEffect(() => {
    const fetchMetrics = async () => {
      try {
        setLoading(true);
        const response = await apiWrapper.Analytics.getFranchiseSeasonMetrics(2025);
        setMetrics(response.data || []);
      } catch (err) {
        setError('Failed to load franchise metrics');
        console.error('Error loading franchise metrics:', err);
      } finally {
        setLoading(false);
      }
    };

    fetchMetrics();
  }, []);

  const handleSort = (column) => {
    if (sortBy === column) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(column);
      setSortOrder('asc');
    }
  };

  const sortedMetrics = [...metrics].sort((a, b) => {
    let aVal = a[sortBy];
    let bVal = b[sortBy];
    
    // Handle string sorting
    if (typeof aVal === 'string') {
      aVal = aVal.toLowerCase();
      bVal = bVal.toLowerCase();
    }
    
    if (sortOrder === 'asc') {
      return aVal > bVal ? 1 : -1;
    } else {
      return aVal < bVal ? 1 : -1;
    }
  });

  // Show only top 10 by default, all if showAll is true
  const displayedMetrics = showAll ? sortedMetrics : sortedMetrics.slice(0, 10);

  const formatValue = (value, key) => {
    // Handle null/undefined values
    if (value === null || value === undefined) return '-';
    
    // Format percentages
    if (key.toLowerCase().includes('rate') || key.toLowerCase().includes('pct') || key === 'timePossRatio') {
      return `${(value * 100).toFixed(1)}%`;
    }
    
    // Format decimal values with appropriate precision
    if (typeof value === 'number') {
      if (key === 'gamesPlayed' || key === 'seasonYear') {
        return value.toString();
      }
      // Field position difference and turnover margin can be negative, show more precision
      if (key === 'fieldPosDiff' || key === 'turnoverMarginPerDrive') {
        return value.toFixed(3);
      }
      // Most other metrics show 2 decimal places
      return value.toFixed(2);
    }
    
    return value;
  };

  const getSortIcon = (column) => {
    if (sortBy !== column) return '↕️';
    return sortOrder === 'asc' ? '↑' : '↓';
  };

  if (loading) {
    return (
      <div className="franchise-metrics-grid">
        <h2>Franchise Season Metrics (2025)</h2>
        <div className="loading-state">Loading franchise metrics...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="franchise-metrics-grid">
        <h2>Franchise Season Metrics (2025)</h2>
        <div className="error-state">{error}</div>
      </div>
    );
  }

  return (
    <div className="franchise-metrics-grid">
      <div className="metrics-header">
        <h2>Franchise Season Metrics (2025)</h2>
        <button 
          className="show-all-btn"
          onClick={() => setShowAll(!showAll)}
        >
          {showAll ? 'Show 10' : 'Show All'}
        </button>
      </div>
      <div className="metrics-table-container">
        <table className="metrics-table">
          <thead>
            <tr>
              <th onClick={() => handleSort('franchiseName')} className="sortable">
                Team {getSortIcon('franchiseName')}
              </th>
              <th onClick={() => handleSort('conference')} className="sortable">
                Conference {getSortIcon('conference')}
              </th>
              <th onClick={() => handleSort('gamesPlayed')} className="sortable">
                GP {getSortIcon('gamesPlayed')}
              </th>
              <th onClick={() => handleSort('ypp')} className="sortable">
                YPP {getSortIcon('ypp')}
              </th>
              <th onClick={() => handleSort('successRate')} className="sortable">
                Success{'\n'}Rate {getSortIcon('successRate')}
              </th>
              <th onClick={() => handleSort('explosiveRate')} className="sortable">
                Explosive{'\n'}Rate {getSortIcon('explosiveRate')}
              </th>
              <th onClick={() => handleSort('pointsPerDrive')} className="sortable">
                PPD {getSortIcon('pointsPerDrive')}
              </th>
              <th onClick={() => handleSort('thirdFourthRate')} className="sortable">
                3rd/4th{'\n'}Rate {getSortIcon('thirdFourthRate')}
              </th>
              <th onClick={() => handleSort('rzTdRate')} className="sortable">
                RZ TD{'\n'}Rate {getSortIcon('rzTdRate')}
              </th>
              <th onClick={() => handleSort('rzScoreRate')} className="sortable">
                RZ Score{'\n'}Rate {getSortIcon('rzScoreRate')}
              </th>
              <th onClick={() => handleSort('timePossRatio')} className="sortable">
                Time{'\n'}Poss {getSortIcon('timePossRatio')}
              </th>
              <th onClick={() => handleSort('oppYpp')} className="sortable">
                Opp{'\n'}YPP {getSortIcon('oppYpp')}
              </th>
              <th onClick={() => handleSort('oppSuccessRate')} className="sortable">
                Opp{'\n'}Success {getSortIcon('oppSuccessRate')}
              </th>
              <th onClick={() => handleSort('oppExplosiveRate')} className="sortable">
                Opp{'\n'}Explosive {getSortIcon('oppExplosiveRate')}
              </th>
              <th onClick={() => handleSort('oppPointsPerDrive')} className="sortable">
                Opp{'\n'}PPD {getSortIcon('oppPointsPerDrive')}
              </th>
              <th onClick={() => handleSort('oppThirdFourthRate')} className="sortable">
                Opp{'\n'}3rd/4th {getSortIcon('oppThirdFourthRate')}
              </th>
              <th onClick={() => handleSort('oppRzTdRate')} className="sortable">
                Opp{'\n'}RZ TD {getSortIcon('oppRzTdRate')}
              </th>
              <th onClick={() => handleSort('oppScoreTdRate')} className="sortable">
                Opp Score{'\n'}TD {getSortIcon('oppScoreTdRate')}
              </th>
              <th onClick={() => handleSort('netPunt')} className="sortable">
                Net{'\n'}Punt {getSortIcon('netPunt')}
              </th>
              <th onClick={() => handleSort('fgPctShrunk')} className="sortable">
                FG% {getSortIcon('fgPctShrunk')}
              </th>
              <th onClick={() => handleSort('fieldPosDiff')} className="sortable">
                Field Pos{'\n'}Diff {getSortIcon('fieldPosDiff')}
              </th>
              <th onClick={() => handleSort('turnoverMarginPerDrive')} className="sortable">
                TO Margin{'\n'}/Drive {getSortIcon('turnoverMarginPerDrive')}
              </th>
              <th onClick={() => handleSort('penaltyYardsPerPlay')} className="sortable">
                Penalty{'\n'}Y/P {getSortIcon('penaltyYardsPerPlay')}
              </th>
            </tr>
          </thead>
          <tbody>
            {displayedMetrics.map((team, index) => (
              <tr key={team.franchiseSlug} className={index % 2 === 0 ? 'even-row' : 'odd-row'}>
                <td className="team-name">{team.franchiseName}</td>
                <td>{team.conference || '-'}</td>
                <td>{formatValue(team.gamesPlayed, 'gamesPlayed')}</td>
                <td>{formatValue(team.ypp, 'ypp')}</td>
                <td>{formatValue(team.successRate, 'successRate')}</td>
                <td>{formatValue(team.explosiveRate, 'explosiveRate')}</td>
                <td>{formatValue(team.pointsPerDrive, 'pointsPerDrive')}</td>
                <td>{formatValue(team.thirdFourthRate, 'thirdFourthRate')}</td>
                <td>{formatValue(team.rzTdRate, 'rzTdRate')}</td>
                <td>{formatValue(team.rzScoreRate, 'rzScoreRate')}</td>
                <td>{formatValue(team.timePossRatio, 'timePossRatio')}</td>
                <td>{formatValue(team.oppYpp, 'oppYpp')}</td>
                <td>{formatValue(team.oppSuccessRate, 'oppSuccessRate')}</td>
                <td>{formatValue(team.oppExplosiveRate, 'oppExplosiveRate')}</td>
                <td>{formatValue(team.oppPointsPerDrive, 'oppPointsPerDrive')}</td>
                <td>{formatValue(team.oppThirdFourthRate, 'oppThirdFourthRate')}</td>
                <td>{formatValue(team.oppRzTdRate, 'oppRzTdRate')}</td>
                <td>{formatValue(team.oppScoreTdRate, 'oppScoreTdRate')}</td>
                <td>{formatValue(team.netPunt, 'netPunt')}</td>
                <td>{formatValue(team.fgPctShrunk, 'fgPctShrunk')}</td>
                <td>{formatValue(team.fieldPosDiff, 'fieldPosDiff')}</td>
                <td>{formatValue(team.turnoverMarginPerDrive, 'turnoverMarginPerDrive')}</td>
                <td>{formatValue(team.penaltyYardsPerPlay, 'penaltyYardsPerPlay')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {metrics.length > 0 && (
        <div className="metrics-summary">
          Showing {displayedMetrics.length} of {metrics.length} teams
          {!showAll && metrics.length > 10 && (
            <span className="top-ten-note"> (10)</span>
          )}
        </div>
      )}
    </div>
  );
}

export default FranchiseMetricsGrid;