import React from 'react';
import './WarRoomPage.css';

function WarRoomPage() {
  return (
    <div className="war-room-page">
      <div className="war-room-header">
        <h1>War Room</h1>
        <p>Command center for advanced analytics and customizable widgets</p>
      </div>
      
      <div className="war-room-content">
        <div className="widget-placeholder">
          <h3>Coming Soon</h3>
          <p>User-customizable widgets for:</p>
          <ul>
            <li>Yards Per Carry (Top 10)</li>
            <li>Points per Game</li>
            <li>Advanced Team Statistics</li>
            <li>Performance Analytics</li>
          </ul>
        </div>
      </div>
    </div>
  );
}

export default WarRoomPage;