import React from 'react';
import FranchiseMetricsGrid from './FranchiseMetricsGrid';
import './WarRoomPage.css';

function WarRoomPage() {
  return (
    <div className="war-room-page">
      <div className="war-room-header">
        <h1>War Room</h1>
        <p>Command center for advanced analytics and customizable widgets</p>
      </div>
      
      <div className="war-room-content">
        <FranchiseMetricsGrid />
        
        <div className="widget-placeholder">
          <h3>More Widgets Coming Soon</h3>
          <p>Additional customizable widgets for:</p>
          <ul>
            <li>Yards Per Carry (Top 10)</li>
            <li>Red Zone Efficiency</li>
            <li>Third Down Conversions</li>
            <li>Turnover Analysis</li>
          </ul>
        </div>
      </div>
    </div>
  );
}

export default WarRoomPage;