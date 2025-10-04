import React from "react";
import "./ContestOverview.css";

export default function ContestOverviewInfo({ info }) {
  if (!info) {
    return (
      <div className="contest-section">
        <div className="contest-section-title">Info</div>
        <div className="contest-info-section">
          <div className="contest-info-list">No info available.</div>
        </div>
      </div>
    );
  }
  return (
    <div className="contest-section">
      <div className="contest-section-title">Info</div>
      <div className="contest-info-section">
        <ul className="contest-info-list">
          {info.venue && (
            <li className="contest-info-item"><span className="contest-info-label">Venue:</span> <span className="contest-info-value">{info.venue}</span></li>
          )}
          {info.venueCity && (
            <li className="contest-info-item"><span className="contest-info-label">City:</span> <span className="contest-info-value">{info.venueCity}</span></li>
          )}
          {info.venueState && (
            <li className="contest-info-item"><span className="contest-info-label">State:</span> <span className="contest-info-value">{info.venueState}</span></li>
          )}
          {info.venueImageUrl && (
            <li className="contest-info-item"><span className="contest-info-label">Image:</span> <img src={info.venueImageUrl} alt="Venue" style={{ maxWidth: 120, maxHeight: 80, borderRadius: 6 }} /></li>
          )}
          {info.startDateUtc && (
            <li className="contest-info-item"><span className="contest-info-label">Start Time (UTC):</span> <span className="contest-info-value">{info.startDateUtc}</span></li>
          )}
          {info.attendance !== undefined && (
            <li className="contest-info-item"><span className="contest-info-label">Attendance:</span> <span className="contest-info-value">{info.attendance}</span></li>
          )}
          {info.broadcast && (
            <li className="contest-info-item"><span className="contest-info-label">Broadcast:</span> <span className="contest-info-value">{info.broadcast}</span></li>
          )}
        </ul>
      </div>
    </div>
  );
}
