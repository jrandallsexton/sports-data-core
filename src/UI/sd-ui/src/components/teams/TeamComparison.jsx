import React, { useState, useEffect } from "react";
import "./TeamComparison.css";
import "./TeamComparisonTabs.css";


/**
 * TeamComparison Dialog
 * Props:
 * - open: boolean (controls dialog visibility)
 * - onClose: function (called to close dialog)
 * - teamA: { name, logoUri, stats: { data: { statistics: { ... } } } }
 * - teamB: { name, logoUri, stats: { data: { statistics: { ... } } } }
 *
 * The stats prop is the full API response, just like TeamStatistics.
 */
export default function TeamComparison({ open, onClose, teamA, teamB }) {
  // Prevent background scroll when dialog is open
  useEffect(() => {
    if (open) {
      const original = document.body.style.overflow;
      document.body.style.overflow = "hidden";
      return () => { document.body.style.overflow = original; };
    }
  }, [open]);
  // Use the same logic as TeamStatistics
  const statisticsA = teamA.stats?.data?.statistics || teamA.stats?.statistics || {};
  const statisticsB = teamB.stats?.data?.statistics || teamB.stats?.statistics || {};
  const categories = Object.keys(statisticsA);
  const [selectedCategory, setSelectedCategory] = useState(categories[0] || "");

  if (!open) return null;

  // Helper to determine which team is favored (higher value wins by default)
  const getFavored = (a, b) => {
    if (a == null || b == null) return null;
    const aNum = parseFloat(a);
    const bNum = parseFloat(b);
    if (!isNaN(aNum) && !isNaN(bNum)) {
      if (aNum > bNum) return "A";
      if (bNum > aNum) return "B";
    }
    return null;
  };

  return (
    <div className="team-comparison-dialog-backdrop" onClick={onClose}>
      <div className="team-comparison-dialog" onClick={e => e.stopPropagation()}>
        <div className="team-comparison-header">
          <div className="team-col">
            <img src={teamA.logoUri} alt={teamA.name} className="team-logo" />
            <div className="team-name">{teamA.name}</div>
          </div>
          <div className="vs-col">vs</div>
          <div className="team-col">
            <img src={teamB.logoUri} alt={teamB.name} className="team-logo" />
            <div className="team-name">{teamB.name}</div>
          </div>
        </div>
        <div className="team-comparison-tabs">
          {categories.map(cat => (
            <button
              key={cat}
              className={`team-comparison-tab${selectedCategory === cat ? " selected" : ""}`}
              onClick={() => setSelectedCategory(cat)}
            >
              {cat.charAt(0).toUpperCase() + cat.slice(1)}
            </button>
          ))}
        </div>
        {selectedCategory && (
          <div className="team-comparison-table">
            {(statisticsA[selectedCategory] || []).map((entry, idx) => {
              const aVal = entry.displayValue ?? "-";
              const bEntry = (statisticsB[selectedCategory] || [])[idx] || {};
              const bVal = bEntry.displayValue ?? "-";
              const statKey = entry.statisticKey;
              const statLabel = entry.statisticValue;
              const favored = getFavored(aVal, bVal);
              return (
                <div className="stat-row" key={statKey}>
                  <div className={`stat-value left${favored === "A" ? " favored" : ""}`}>{aVal}</div>
                  <div className="stat-category" style={{ width: 480, minWidth: 360, maxWidth: 660, textOverflow: 'ellipsis', overflow: 'hidden', whiteSpace: 'nowrap' }}>{statLabel}</div>
                  <div className={`stat-value right${favored === "B" ? " favored" : ""}`}>{bVal}</div>
                </div>
              );
            })}
          </div>
        )}
        <button className="close-btn" onClick={onClose}>Close</button>
      </div>
    </div>
  );
}
