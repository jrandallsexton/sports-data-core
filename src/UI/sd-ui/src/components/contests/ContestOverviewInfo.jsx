import React from "react";
import { formatToEasternTime } from "../../utils/timeUtils";
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
          {/* Venue image will be shown after the list */}
          {info.startDateUtc && (
            <li className="contest-info-item">
              <span className="contest-info-label">Start Time (ET):</span> 
              <span className="contest-info-value">{formatToEasternTime(info.startDateUtc)}</span>
            </li>
          )}
          {info.attendance !== undefined && (
            <li className="contest-info-item"><span className="contest-info-label">Attendance:</span> <span className="contest-info-value">{info.attendance}</span></li>
          )}
          {info.broadcast && (
            <li className="contest-info-item"><span className="contest-info-label">Broadcast:</span> <span className="contest-info-value">{info.broadcast}</span></li>
          )}
        </ul>
        {info.venueImageUrl && (
          <div style={{ marginTop: 8, textAlign: 'center', width: '100%' }}>
            <img 
              src={info.venueImageUrl} 
              alt="Venue" 
              style={{ width: '100%', height: 'auto', borderRadius: 6, objectFit: 'cover', maxHeight: 220, marginBottom: 0, paddingBottom: 0 }} 
            />
          </div>
        )}
      </div>
    </div>
  );
}
