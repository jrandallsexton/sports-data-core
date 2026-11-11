import React from 'react';
import FranchiseMetricsGrid from './FranchiseMetricsGrid';
import LeadersPanel from './LeadersPanel';
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
        <LeadersPanel />
      </div>
    </div>
  );
}

export default WarRoomPage;
