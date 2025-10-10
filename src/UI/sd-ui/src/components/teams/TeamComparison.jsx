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
export default function TeamComparison({ open, onClose, teamA, teamB, teamAColor = '#61dafb', teamBColor = '#61dafb' }) {
  // Helper: choose light or dark text based on background color
  const getContrastTextColor = (bgColor) => {
    let color = bgColor;
    if (typeof color === 'string' && color.length === 6 && !color.startsWith('#')) {
      color = `#${color}`;
    }
    if (color.startsWith('#')) {
      let hex = color.replace('#', '');
      if (hex.length === 3) {
        hex = hex.split('').map(x => x + x).join('');
      }
      if (hex.length === 6) {
        const r = parseInt(hex.substring(0,2), 16);
        const g = parseInt(hex.substring(2,4), 16);
        const b = parseInt(hex.substring(4,6), 16);
        // Relative luminance formula
        const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return luminance < 128 ? '#fff' : '#23272f';
      }
    }
    // For rgb(a) colors
    if (color.startsWith('rgb')) {
      const vals = color.match(/\d+/g);
      if (vals && vals.length >= 3) {
        const r = parseInt(vals[0], 10);
        const g = parseInt(vals[1], 10);
        const b = parseInt(vals[2], 10);
        const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return luminance < 128 ? '#fff' : '#23272f';
      }
    }
    // Default to dark text
    return '#23272f';
  };
  // Normalize color: prepend # if missing for hex
  const normalizeColor = (color) => {
    if (typeof color === 'string' && color.length === 6 && !color.startsWith('#')) {
      return `#${color}`;
    }
    return color;
  };
  const normAColor = normalizeColor(teamAColor);
  const normBColor = normalizeColor(teamBColor);
  // Debug: log normalized color props
  console.log('TeamComparison colors:', { teamAColor, teamBColor, normAColor, normBColor });
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
  // Helper to determine which team is favored (supports isNegativeAttribute)
  const getFavored = (a, b, aEntry = {}, bEntry = {}) => {
    if (a == null || b == null) return null;
    const aNum = parseFloat(a);
    const bNum = parseFloat(b);
    // Use isNegativeAttribute from either entry (prefer A, fallback to B)
    const isNegative = aEntry.isNegativeAttribute ?? bEntry.isNegativeAttribute ?? false;
    if (!isNaN(aNum) && !isNaN(bNum)) {
      if (isNegative) {
        if (aNum < bNum) return "A";
        if (bNum < aNum) return "B";
      } else {
        if (aNum > bNum) return "A";
        if (bNum > aNum) return "B";
      }
    }
    return null;
  };

  // Helper to get muted color (simple alpha blend)
  const getMutedColor = (color) => {
    if (!color) return '#61dafb33';
    // If hex, convert to rgba with alpha
    if (color.startsWith('#')) {
      // Support #RRGGBB and #RGB
      let hex = color.replace('#', '');
      if (hex.length === 3) {
        hex = hex.split('').map(x => x + x).join('');
      }
      if (hex.length === 6) {
        const r = parseInt(hex.substring(0,2), 16);
        const g = parseInt(hex.substring(2,4), 16);
        const b = parseInt(hex.substring(4,6), 16);
        return `rgba(${r},${g},${b},0.18)`;
      }
      // Fallback for other hex formats
      return color;
    }
    // If rgb(a), reduce alpha
    if (color.startsWith('rgb')) {
      // If already rgba, replace alpha
      if (color.startsWith('rgba')) {
        return color.replace(/rgba\(([^,]+),([^,]+),([^,]+),[^)]+\)/, 'rgba($1,$2,$3,0.18)');
      }
      // If rgb, add alpha
      return color.replace(/rgb\(([^)]+)\)/, 'rgba($1,0.18)');
    }
    // Otherwise, use color as-is
    return color;
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
          {categories.map(cat => {
            // Count favored stats for each team in this category
            const statsA = statisticsA[cat] || [];
            const statsB = statisticsB[cat] || [];
            let favoredA = 0, favoredB = 0;
            for (let i = 0; i < Math.max(statsA.length, statsB.length); i++) {
              const entryA = statsA[i] || {};
              const entryB = statsB[i] || {};
              const favored = getFavored(entryA.displayValue ?? '-', entryB.displayValue ?? '-', entryA, entryB);
              if (favored === 'A') favoredA++;
              if (favored === 'B') favoredB++;
            }
            let tabBg = '';
            let tabColor = '';
            if (favoredA > favoredB) {
              tabBg = (/^#|rgb/.test(normAColor) ? normAColor : getMutedColor(normAColor));
              tabColor = getContrastTextColor(normAColor);
            } else if (favoredB > favoredA) {
              tabBg = (/^#|rgb/.test(normBColor) ? normBColor : getMutedColor(normBColor));
              tabColor = getContrastTextColor(normBColor);
            } else {
              tabBg = '#343a40';
              tabColor = '#fff';
            }
            return (
              <button
                key={cat}
                className={`team-comparison-tab${selectedCategory === cat ? " selected" : ""}`}
                onClick={() => setSelectedCategory(cat)}
                style={{
                  background: tabBg,
                  color: tabColor,
                  borderRadius: 6,
                  fontWeight: selectedCategory === cat ? 'bold' : undefined,
                  position: 'relative',
                  zIndex: selectedCategory === cat ? 2 : 1,
                  display: 'flex',
                  alignItems: 'center',
                  gap: selectedCategory === cat ? 6 : 0
                }}
              >
                {selectedCategory === cat && (
                  <span style={{fontSize:'1.1em', verticalAlign:'middle'}}>★</span>
                )}
                {cat.charAt(0).toUpperCase() + cat.slice(1)}
              </button>
            );
          })}
        </div>
        {selectedCategory && (
          <div className="team-comparison-table">
            {(statisticsA[selectedCategory] || []).map((entry, idx) => {
              const bEntry = (statisticsB[selectedCategory] || [])[idx] || {};
              const favored = getFavored(entry.displayValue ?? "-", bEntry.displayValue ?? "-", entry, bEntry);
              const aRankContent = entry.rank && entry.rank > 1 ? (
                <span className="rank-inline">(#{entry.rank})</span>
              ) : <span style={{ width: 0, display: 'inline-block' }}></span>;
              const bRankContent = bEntry.rank && bEntry.rank > 1 ? (
                <span className="rank-inline">(#{bEntry.rank})</span>
              ) : <span style={{ width: 0, display: 'inline-block' }}></span>;
              const aValContent = entry.displayValue ?? "-";
              const bValContent = bEntry.displayValue ?? "-";
              const statKey = entry.statisticKey;
              const statLabel = entry.statisticValue;
              return (
                <div className="stat-row" key={statKey}>
                  <div className="stat-rank left-rank">{aRankContent}</div>
                  <div
                    className={`stat-value left${favored === "A" ? " favored" : ""}`}
                    style={favored === "A" ? { background: (/^#|rgb/.test(normAColor) ? normAColor : getMutedColor(normAColor)), borderRadius: 6, color: getContrastTextColor(normAColor) } : {}}
                  >{aValContent}</div>
                  <div className="stat-category" style={{ width: 480, minWidth: 360, maxWidth: 660, textOverflow: 'ellipsis', overflow: 'hidden', whiteSpace: 'nowrap' }}>{statLabel}</div>
                  <div
                    className={`stat-value right${favored === "B" ? " favored" : ""}`}
                    style={favored === "B" ? { background: (/^#|rgb/.test(normBColor) ? normBColor : getMutedColor(normBColor)), borderRadius: 6, color: getContrastTextColor(normBColor) } : {}}
                  >{bValContent}</div>
                  <div className="stat-rank right-rank">{bRankContent}</div>
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
