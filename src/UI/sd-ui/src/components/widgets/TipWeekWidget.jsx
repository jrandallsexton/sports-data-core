import React from "react";
import "../home/HomePage.css";

function TipWeekWidget() {
  return (
    <div className="tip-card">
      <h2>Tip of the Week (simulated)</h2>
      <p>
        <strong>💡 Pro Strategy:</strong> Balance underdogs and favorites! Most users win more by mixing picks based on matchup strength, not just spreads.
      </p>
      <p className="tip-note">
        Check this week's spread info before making final picks.
      </p>
    </div>
  );
}

export default TipWeekWidget;
