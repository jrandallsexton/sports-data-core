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
  const [selectedConference, setSelectedConference] = useState('all');
  const [selectedRow, setSelectedRow] = useState(null);

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
      setSortOrder('desc');
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

  // Filter by conference first, then apply sorting
  const filteredMetrics = selectedConference === 'all' 
    ? sortedMetrics 
    : sortedMetrics.filter(team => team.conference === selectedConference);

  // Show only top 10 by default when showing all conferences, show all when conference is filtered
  const shouldShowAll = showAll || selectedConference !== 'all';
  const displayedMetrics = shouldShowAll ? filteredMetrics : filteredMetrics.slice(0, 10);

  // Get unique conferences for dropdown
  const conferences = [...new Set(metrics.map(team => team.conference).filter(Boolean))].sort();

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

      // Integer metrics (min/max/counts) - only non-average values
      if ((key.includes('Min') || key.includes('Max')) && !key.includes('Avg')) {
        return Math.round(value).toString();
      }

      // Average metrics show 2 decimal places (includes ptsScoredAvg, ptsAllowedAvg, marginWinAvg, marginLossAvg)
      if (key.includes('Avg')) {
        return value.toFixed(2);
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
        <div className="header-controls">
          <select 
            value={selectedConference} 
            onChange={(e) => setSelectedConference(e.target.value)}
            className="conference-filter"
          >
            <option value="all">All Conferences</option>
            {conferences.map(conference => (
              <option key={conference} value={conference}>
                {conference}
              </option>
            ))}
          </select>
          <button 
            className="show-all-btn"
            onClick={() => setShowAll(!showAll)}
            disabled={selectedConference !== 'all'}
            style={{ 
              opacity: selectedConference !== 'all' ? 0.5 : 1,
              cursor: selectedConference !== 'all' ? 'not-allowed' : 'pointer'
            }}
          >
            {showAll ? 'Show 10' : 'Show All'}
          </button>
        </div>
      </div>
      <div className="metrics-table-container">
        <table className="metrics-table">
          <thead>
            <tr>
              <th onClick={() => handleSort('franchiseName')} className="sortable">
                Team {getSortIcon('franchiseName')}
              </th>
              {selectedConference === 'all' && (
                <th className="conference-header">
                  Conference
                </th>
              )}
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
              <th onClick={() => handleSort('ptsScoredMin')} className="sortable">
                Pts Scored{'\n'}Min {getSortIcon('ptsScoredMin')}
              </th>
              <th onClick={() => handleSort('ptsScoredMax')} className="sortable">
                Pts Scored{'\n'}Max {getSortIcon('ptsScoredMax')}
              </th>
              <th onClick={() => handleSort('ptsScoredAvg')} className="sortable">
                Pts Scored{'\n'}Avg {getSortIcon('ptsScoredAvg')}
              </th>
              <th onClick={() => handleSort('ptsAllowedMin')} className="sortable">
                Pts Allowed{'\n'}Min {getSortIcon('ptsAllowedMin')}
              </th>
              <th onClick={() => handleSort('ptsAllowedMax')} className="sortable">
                Pts Allowed{'\n'}Max {getSortIcon('ptsAllowedMax')}
              </th>
              <th onClick={() => handleSort('ptsAllowedAvg')} className="sortable">
                Pts Allowed{'\n'}Avg {getSortIcon('ptsAllowedAvg')}
              </th>
              <th onClick={() => handleSort('marginWinMin')} className="sortable">
                Win Margin{'\n'}Min {getSortIcon('marginWinMin')}
              </th>
              <th onClick={() => handleSort('marginWinMax')} className="sortable">
                Win Margin{'\n'}Max {getSortIcon('marginWinMax')}
              </th>
              <th onClick={() => handleSort('marginWinAvg')} className="sortable">
                Win Margin{'\n'}Avg {getSortIcon('marginWinAvg')}
              </th>
              <th onClick={() => handleSort('marginLossMin')} className="sortable">
                Loss Margin{'\n'}Min {getSortIcon('marginLossMin')}
              </th>
              <th onClick={() => handleSort('marginLossMax')} className="sortable">
                Loss Margin{'\n'}Max {getSortIcon('marginLossMax')}
              </th>
              <th onClick={() => handleSort('marginLossAvg')} className="sortable">
                Loss Margin{'\n'}Avg {getSortIcon('marginLossAvg')}
              </th>
            </tr>
          </thead>
          <tbody>
            {displayedMetrics.map((team, index) => (
              <tr 
                key={team.franchiseSlug} 
                className={`${index % 2 === 0 ? 'even-row' : 'odd-row'} ${selectedRow === team.franchiseSlug ? 'selected-row' : ''}`}
                onClick={() => setSelectedRow(selectedRow === team.franchiseSlug ? null : team.franchiseSlug)}
                style={{ cursor: 'pointer' }}
              >
                <td className="team-name">
                  <a
                    href={`/app/sport/football/ncaa/team/${team.franchiseSlug}/2025`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="team-link"
                  >
                    {team.franchiseName}
                  </a>
                </td>
                {selectedConference === 'all' && (
                  <td>{team.conference || '-'}</td>
                )}
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
                <td>{formatValue(team.ptsScoredMin, 'ptsScoredMin')}</td>
                <td>{formatValue(team.ptsScoredMax, 'ptsScoredMax')}</td>
                <td>{formatValue(team.ptsScoredAvg, 'ptsScoredAvg')}</td>
                <td>{formatValue(team.ptsAllowedMin, 'ptsAllowedMin')}</td>
                <td>{formatValue(team.ptsAllowedMax, 'ptsAllowedMax')}</td>
                <td>{formatValue(team.ptsAllowedAvg, 'ptsAllowedAvg')}</td>
                <td>{formatValue(team.marginWinMin, 'marginWinMin')}</td>
                <td>{formatValue(team.marginWinMax, 'marginWinMax')}</td>
                <td>{formatValue(team.marginWinAvg, 'marginWinAvg')}</td>
                <td>{formatValue(team.marginLossMin, 'marginLossMin')}</td>
                <td>{formatValue(team.marginLossMax, 'marginLossMax')}</td>
                <td>{formatValue(team.marginLossAvg, 'marginLossAvg')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {metrics.length > 0 && (
        <div className="metrics-summary">
          Showing {displayedMetrics.length} of {filteredMetrics.length} teams
          {selectedConference !== 'all' && ` in ${selectedConference}`}
          {!shouldShowAll && filteredMetrics.length > 10 && (
            <span className="top-ten-note"> (top 10)</span>
          )}
        </div>
      )}
    </div>
  );
}

export default FranchiseMetricsGrid;