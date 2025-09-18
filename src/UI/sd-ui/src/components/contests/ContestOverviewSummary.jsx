import React from "react";
import "./ContestOverview.css";

export default function ContestOverviewSummary({ summary }) {
  return (
    <div className="contest-section">
      <div className="contest-section-title">Summary</div>
      <div className="contest-summary-section">
        <div className="contest-summary-preview">{summary.previewText}</div>
        <div className="contest-summary-result">{summary.resultText}</div>
      </div>
    </div>
  );
}
