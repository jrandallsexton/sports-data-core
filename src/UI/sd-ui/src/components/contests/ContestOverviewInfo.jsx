import React from "react";
import "./ContestOverview.css";

export default function ContestOverviewInfo({ info }) {
  if (!Array.isArray(info)) {
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
          {info.map((item, idx) => (
            <li key={idx} className="contest-info-item">
              <span className="contest-info-label">{item.label}:</span>{" "}
              <span className="contest-info-value">{item.value}</span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
