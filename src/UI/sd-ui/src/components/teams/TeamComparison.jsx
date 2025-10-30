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
export default function TeamComparison({
  open,
  onClose,
  teamA,
  teamB,
  teamAColor = "#61dafb",
  teamBColor = "#61dafb",
}) {
  // Main tab state
  const [activeTab, setActiveTab] = useState("statistics");

  // Helper: choose light or dark text based on background color
  const getContrastTextColor = (bgColor) => {
    let color = bgColor;
    if (
      typeof color === "string" &&
      color.length === 6 &&
      !color.startsWith("#")
    ) {
      color = `#${color}`;
    }
    if (color.startsWith("#")) {
      let hex = color.replace("#", "");
      if (hex.length === 3) {
        hex = hex
          .split("")
          .map((x) => x + x)
          .join("");
      }
      if (hex.length === 6) {
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        // Relative luminance formula
        const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return luminance < 128 ? "#fff" : "#23272f";
      }
    }
    // For rgb(a) colors
    if (color.startsWith("rgb")) {
      const vals = color.match(/\d+/g);
      if (vals && vals.length >= 3) {
        const r = parseInt(vals[0], 10);
        const g = parseInt(vals[1], 10);
        const b = parseInt(vals[2], 10);
        const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return luminance < 128 ? "#fff" : "#23272f";
      }
    }
    // Default to dark text
    return "#23272f";
  };
  // Normalize color: prepend # if missing for hex
  const normalizeColor = (color) => {
    if (
      typeof color === "string" &&
      color.length === 6 &&
      !color.startsWith("#")
    ) {
      return `#${color}`;
    }
    return color;
  };
  const normAColor = normalizeColor(teamAColor);
  const normBColor = normalizeColor(teamBColor);
  // Debug: log normalized color props
  console.log("TeamComparison colors:", {
    teamAColor,
    teamBColor,
    normAColor,
    normBColor,
  });
  // Prevent background scroll when dialog is open
  useEffect(() => {
    if (open) {
      const original = document.body.style.overflow;
      document.body.style.overflow = "hidden";
      return () => {
        document.body.style.overflow = original;
      };
    }
  }, [open]);
  // Use the same logic as TeamStatistics
  const statisticsA =
    teamA.stats?.data?.statistics || teamA.stats?.statistics || {};
  const statisticsB =
    teamB.stats?.data?.statistics || teamB.stats?.statistics || {};
  const categories = Object.keys(statisticsA);
  const [selectedCategory, setSelectedCategory] = useState(categories[0] || "");

  // Helper to render the Statistics tab content
  const renderStatisticsTab = () => {
    return (
      <>
        <div className="team-comparison-tabs">
          {categories.map((cat) => {
            // Count favored stats for each team in this category
            const statsA = statisticsA[cat] || [];
            const statsB = statisticsB[cat] || [];
            let favoredA = 0,
              favoredB = 0;
            for (let i = 0; i < Math.max(statsA.length, statsB.length); i++) {
              const entryA = statsA[i] || {};
              const entryB = statsB[i] || {};
              const favored = getFavored(
                entryA.displayValue ?? "-",
                entryB.displayValue ?? "-",
                entryA,
                entryB
              );
              if (favored === "A") favoredA++;
              if (favored === "B") favoredB++;
            }
            let tabBg = "";
            let tabColor = "";
            if (favoredA > favoredB) {
              tabBg = /^#|rgb/.test(normAColor)
                ? normAColor
                : getMutedColor(normAColor);
              tabColor = getContrastTextColor(normAColor);
            } else if (favoredB > favoredA) {
              tabBg = /^#|rgb/.test(normBColor)
                ? normBColor
                : getMutedColor(normBColor);
              tabColor = getContrastTextColor(normBColor);
            } else {
              tabBg = "#343a40";
              tabColor = "#fff";
            }
            return (
              <button
                key={cat}
                className={`team-comparison-tab${
                  selectedCategory === cat ? " selected" : ""
                }`}
                onClick={() => setSelectedCategory(cat)}
                style={{
                  background: tabBg,
                  color: tabColor,
                  borderRadius: 6,
                  fontWeight: selectedCategory === cat ? "bold" : undefined,
                  position: "relative",
                  zIndex: selectedCategory === cat ? 2 : 1,
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  gap: selectedCategory === cat ? 4 : 3,
                  minHeight: "42px",
                  padding: "0.4rem 0.7rem",
                }}
              >
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: selectedCategory === cat ? 6 : 0,
                  }}
                >
                  {selectedCategory === cat && (
                    <span
                      style={{ fontSize: "1.1em", verticalAlign: "middle" }}
                    >
                      ★
                    </span>
                  )}
                  <span>
                    {cat.charAt(0).toUpperCase() + cat.slice(1)} ({favoredA}:
                    {favoredB})
                  </span>
                </div>
                {(favoredA > 0 || favoredB > 0) && (
                  <div className="category-gradient-bar">
                    <div
                      className="gradient-segment team-a"
                      style={{
                        width: `${(favoredA / (favoredA + favoredB)) * 100}%`,
                        backgroundColor: normAColor,
                      }}
                    ></div>
                    <div
                      className="gradient-segment team-b"
                      style={{
                        width: `${(favoredB / (favoredA + favoredB)) * 100}%`,
                        backgroundColor: normBColor,
                      }}
                    ></div>
                  </div>
                )}
              </button>
            );
          })}
        </div>
        {selectedCategory && (
          <div className="team-comparison-table">
            {(statisticsA[selectedCategory] || []).map((entry, idx) => {
              const bEntry = (statisticsB[selectedCategory] || [])[idx] || {};
              const favored = getFavored(
                entry.displayValue ?? "-",
                bEntry.displayValue ?? "-",
                entry,
                bEntry
              );
              const aRankContent =
                entry.rank && entry.rank > 1 ? (
                  <span className="rank-inline">(#{entry.rank})</span>
                ) : (
                  <span style={{ width: 0, display: "inline-block" }}></span>
                );
              const bRankContent =
                bEntry.rank && bEntry.rank > 1 ? (
                  <span className="rank-inline">(#{bEntry.rank})</span>
                ) : (
                  <span style={{ width: 0, display: "inline-block" }}></span>
                );
              const aValContent = entry.displayValue ?? "-";
              const bValContent = bEntry.displayValue ?? "-";
              const statKey = entry.statisticKey;
              const statLabel = entry.statisticValue;
              return (
                <div className="stat-row" key={statKey}>
                  <div className="stat-rank left-rank">{aRankContent}</div>
                  <div
                    className={`stat-value left${
                      favored === "A" ? " favored" : ""
                    }`}
                    style={
                      favored === "A"
                        ? {
                            background: /^#|rgb/.test(normAColor)
                              ? normAColor
                              : getMutedColor(normAColor),
                            borderRadius: 6,
                            color: getContrastTextColor(normAColor),
                          }
                        : {}
                    }
                  >
                    {aValContent}
                  </div>
                  <div
                    className="stat-category"
                    style={{
                      width: 480,
                      minWidth: 360,
                      maxWidth: 660,
                      textOverflow: "ellipsis",
                      overflow: "hidden",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {statLabel}
                  </div>
                  <div
                    className={`stat-value right${
                      favored === "B" ? " favored" : ""
                    }`}
                    style={
                      favored === "B"
                        ? {
                            background: /^#|rgb/.test(normBColor)
                              ? normBColor
                              : getMutedColor(normBColor),
                            borderRadius: 6,
                            color: getContrastTextColor(normBColor),
                          }
                        : {}
                    }
                  >
                    {bValContent}
                  </div>
                  <div className="stat-rank right-rank">{bRankContent}</div>
                </div>
              );
            })}
          </div>
        )}
      </>
    );
  };

  // Helper to render the Metrics tab content
  const renderMetricsTab = () => {
    // Check if metrics data is available
    if (!teamA?.metrics || !teamB?.metrics) {
      return (
        <div className="metrics-placeholder">
          <p
            style={{
              textAlign: "center",
              color: "#adb5bd",
              fontSize: "1.1rem",
              padding: "2rem",
            }}
          >
            Metrics data is not available for comparison.
          </p>
        </div>
      );
    }

    const metricsData = [
      {
        category: "Offensive Efficiency",
        metrics: [
          { 
            label: "Yards Per Play", 
            keyA: "ypp", 
            keyB: "ypp",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: true
          },
          { 
            label: "Success Rate", 
            keyA: "successRate", 
            keyB: "successRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          },
          { 
            label: "Explosive Play Rate", 
            keyA: "explosiveRate", 
            keyB: "explosiveRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          },
          { 
            label: "Points Per Drive", 
            keyA: "pointsPerDrive", 
            keyB: "pointsPerDrive",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: true
          },
          { 
            label: "3rd/4th Down Rate", 
            keyA: "thirdFourthRate", 
            keyB: "thirdFourthRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          }
        ]
      },
      {
        category: "Red Zone Efficiency",
        metrics: [
          { 
            label: "Red Zone TD Rate", 
            keyA: "rzTdRate", 
            keyB: "rzTdRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          },
          { 
            label: "Red Zone Score Rate", 
            keyA: "rzScoreRate", 
            keyB: "rzScoreRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          }
        ]
      },
      {
        category: "Defensive Metrics",
        metrics: [
          { 
            label: "Opp Yards Per Play", 
            keyA: "oppYpp", 
            keyB: "oppYpp",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: false
          },
          { 
            label: "Opp Success Rate", 
            keyA: "oppSuccessRate", 
            keyB: "oppSuccessRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: false
          },
          { 
            label: "Opp Explosive Rate", 
            keyA: "oppExplosiveRate", 
            keyB: "oppExplosiveRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: false
          },
          { 
            label: "Opp Points Per Drive", 
            keyA: "oppPointsPerDrive", 
            keyB: "oppPointsPerDrive",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: false
          },
          { 
            label: "Opp 3rd/4th Down Rate", 
            keyA: "oppThirdFourthRate", 
            keyB: "oppThirdFourthRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: false
          },
          { 
            label: "Opp Red Zone TD Rate", 
            keyA: "oppRzTdRate", 
            keyB: "oppRzTdRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: false
          }
        ]
      },
      {
        category: "Game Control",
        metrics: [
          { 
            label: "Time Possession Ratio", 
            keyA: "timePossRatio", 
            keyB: "timePossRatio",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          },
          { 
            label: "Field Position Differential", 
            keyA: "fieldPosDiff", 
            keyB: "fieldPosDiff",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: true
          },
          { 
            label: "Turnover Margin Per Drive", 
            keyA: "turnoverMarginPerDrive", 
            keyB: "turnoverMarginPerDrive",
            format: (val) => val?.toFixed(3) || "0.000",
            higherIsBetter: true
          }
        ]
      },
      {
        category: "Special Teams",
        metrics: [
          { 
            label: "Net Punting", 
            keyA: "netPunt", 
            keyB: "netPunt",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: true
          },
          { 
            label: "Field Goal %", 
            keyA: "fgPctShrunk", 
            keyB: "fgPctShrunk",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          },
          { 
            label: "Penalty Yards Per Play", 
            keyA: "penaltyYardsPerPlay", 
            keyB: "penaltyYardsPerPlay",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: false
          }
        ]
      }
    ];

    const getMetricFavored = (metric, valA, valB) => {
      if (valA == null || valB == null) return null;
      
      if (metric.higherIsBetter) {
        return valA > valB ? 'A' : valB > valA ? 'B' : null;
      } else {
        return valA < valB ? 'A' : valB < valA ? 'B' : null;
      }
    };

    return (
      <div className="metrics-content">
        <div className="metrics-table">
          {metricsData.map((category, categoryIndex) => 
            category.metrics.map((metric, metricIndex) => {
              const valA = teamA.metrics[metric.keyA];
              const valB = teamB.metrics[metric.keyB];
              const favored = getMetricFavored(metric, valA, valB);

              return (
                <div key={`${categoryIndex}-${metricIndex}`} className="stat-row">
                  <div className="stat-rank left-rank"></div>
                  <div 
                    className={`stat-value left ${favored === 'A' ? 'favored' : ''}`}
                    style={{
                      backgroundColor: favored === 'A' ? normAColor : 'transparent',
                      color: favored === 'A' ? getContrastTextColor(normAColor) : '#f8f9fa'
                    }}
                  >
                    {metric.format(valA)}
                  </div>
                  <div 
                    className="stat-category"
                    style={{
                      width: 480,
                      minWidth: 360,
                      maxWidth: 660,
                      textOverflow: "ellipsis",
                      overflow: "hidden",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {metric.label}
                  </div>
                  <div 
                    className={`stat-value right ${favored === 'B' ? 'favored' : ''}`}
                    style={{
                      backgroundColor: favored === 'B' ? normBColor : 'transparent',
                      color: favored === 'B' ? getContrastTextColor(normBColor) : '#f8f9fa'
                    }}
                  >
                    {metric.format(valB)}
                  </div>
                  <div className="stat-rank right-rank"></div>
                </div>
              );
            })
          )}
        </div>
      </div>
    );
  };

  if (!open) return null;

  // Helper to determine which team is favored (higher value wins by default)
  // Helper to determine which team is favored (supports isNegativeAttribute)
  const getFavored = (a, b, aEntry = {}, bEntry = {}) => {
    if (a == null || b == null) return null;
    const aNum = parseFloat(a);
    const bNum = parseFloat(b);
    // Use isNegativeAttribute from either entry (prefer A, fallback to B)
    const isNegative =
      aEntry.isNegativeAttribute ?? bEntry.isNegativeAttribute ?? false;
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

  // Calculate overall team favorability across all statistics for main tab coloring
  const calculateOverallFavorability = () => {
    let totalFavoredA = 0,
      totalFavoredB = 0;

    categories.forEach((cat) => {
      const statsA = statisticsA[cat] || [];
      const statsB = statisticsB[cat] || [];
      for (let i = 0; i < Math.max(statsA.length, statsB.length); i++) {
        const entryA = statsA[i] || {};
        const entryB = statsB[i] || {};
        const favored = getFavored(
          entryA.displayValue ?? "-",
          entryB.displayValue ?? "-",
          entryA,
          entryB
        );
        if (favored === "A") totalFavoredA++;
        if (favored === "B") totalFavoredB++;
      }
    });

    return { totalFavoredA, totalFavoredB };
  };

  const calculateMetricsFavorability = () => {
    if (!teamA?.metrics || !teamB?.metrics) {
      return { metricsFavoredA: 0, metricsFavoredB: 0 };
    }

    let metricsFavoredA = 0,
      metricsFavoredB = 0;

    // Define all metrics with their comparison logic
    const metricsToCompare = [
      // Offensive metrics (higher is better)
      { key: "ypp", higherIsBetter: true },
      { key: "successRate", higherIsBetter: true },
      { key: "explosiveRate", higherIsBetter: true },
      { key: "pointsPerDrive", higherIsBetter: true },
      { key: "thirdFourthRate", higherIsBetter: true },
      { key: "rzTdRate", higherIsBetter: true },
      { key: "rzScoreRate", higherIsBetter: true },
      // Defensive metrics (lower is better for opponent stats)
      { key: "oppYpp", higherIsBetter: false },
      { key: "oppSuccessRate", higherIsBetter: false },
      { key: "oppExplosiveRate", higherIsBetter: false },
      { key: "oppPointsPerDrive", higherIsBetter: false },
      { key: "oppThirdFourthRate", higherIsBetter: false },
      { key: "oppRzTdRate", higherIsBetter: false },
      // Game control metrics
      { key: "timePossRatio", higherIsBetter: true },
      { key: "fieldPosDiff", higherIsBetter: true },
      { key: "turnoverMarginPerDrive", higherIsBetter: true },
      // Special teams metrics
      { key: "netPunt", higherIsBetter: true },
      { key: "fgPctShrunk", higherIsBetter: true },
      { key: "penaltyYardsPerPlay", higherIsBetter: false }
    ];

    metricsToCompare.forEach((metric) => {
      const valA = teamA.metrics[metric.key];
      const valB = teamB.metrics[metric.key];
      
      if (valA != null && valB != null) {
        if (metric.higherIsBetter) {
          if (valA > valB) metricsFavoredA++;
          else if (valB > valA) metricsFavoredB++;
        } else {
          if (valA < valB) metricsFavoredA++;
          else if (valB < valA) metricsFavoredB++;
        }
      }
    });

    return { metricsFavoredA, metricsFavoredB };
  };

  const { totalFavoredA, totalFavoredB } = calculateOverallFavorability();
  const { metricsFavoredA, metricsFavoredB } = calculateMetricsFavorability();

  // Get main tab styling based on overall favorability
  const getMainTabStyling = (tabType, isActive) => {
    if (!isActive) return {}; // Only apply styling to active tabs

    if (tabType === "statistics") {
      if (totalFavoredA > totalFavoredB) {
        return {
          background: /^#|rgb/.test(normAColor)
            ? normAColor
            : getMutedColor(normAColor),
          color: getContrastTextColor(normAColor),
        };
      } else if (totalFavoredB > totalFavoredA) {
        return {
          background: /^#|rgb/.test(normBColor)
            ? normBColor
            : getMutedColor(normBColor),
          color: getContrastTextColor(normBColor),
        };
      }
    } else if (tabType === "metrics") {
      if (metricsFavoredA > metricsFavoredB) {
        return {
          background: /^#|rgb/.test(normAColor)
            ? normAColor
            : getMutedColor(normAColor),
          color: getContrastTextColor(normAColor),
        };
      } else if (metricsFavoredB > metricsFavoredA) {
        return {
          background: /^#|rgb/.test(normBColor)
            ? normBColor
            : getMutedColor(normBColor),
          color: getContrastTextColor(normBColor),
        };
      }
    }
    // Default styling for neutral tabs or when teams are tied
    return {
      background: "#61dafb", // Use default active color
      color: "#23272f",
    };
  };

  // Helper to get muted color (simple alpha blend)
  const getMutedColor = (color) => {
    if (!color) return "#61dafb33";
    // If hex, convert to rgba with alpha
    if (color.startsWith("#")) {
      // Support #RRGGBB and #RGB
      let hex = color.replace("#", "");
      if (hex.length === 3) {
        hex = hex
          .split("")
          .map((x) => x + x)
          .join("");
      }
      if (hex.length === 6) {
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        return `rgba(${r},${g},${b},0.18)`;
      }
      // Fallback for other hex formats
      return color;
    }
    // If rgb(a), reduce alpha
    if (color.startsWith("rgb")) {
      // If already rgba, replace alpha
      if (color.startsWith("rgba")) {
        return color.replace(
          /rgba\(([^,]+),([^,]+),([^,]+),[^)]+\)/,
          "rgba($1,$2,$3,0.18)"
        );
      }
      // If rgb, add alpha
      return color.replace(/rgb\(([^)]+)\)/, "rgba($1,0.18)");
    }
    // Otherwise, use color as-is
    return color;
  };

  return (
    <div className="team-comparison-dialog-backdrop" onClick={onClose}>
      <div
        className="team-comparison-dialog"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="team-comparison-content">
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

          {/* Main tabs */}
          <div className="main-tabs">
            <button
              className={`main-tab ${
                activeTab === "statistics" ? "active" : ""
              }`}
              onClick={() => setActiveTab("statistics")}
              style={getMainTabStyling(
                "statistics",
                activeTab === "statistics"
              )}
            >
              <div className="main-tab-content">
                <div className="tab-text">
                  Statistics ({totalFavoredA}:{totalFavoredB})
                </div>
                {(totalFavoredA > 0 || totalFavoredB > 0) && (
                  <div className="tab-gradient-bar">
                    <div
                      className="gradient-segment team-a"
                      style={{
                        width: `${
                          (totalFavoredA / (totalFavoredA + totalFavoredB)) *
                          100
                        }%`,
                        backgroundColor: normAColor,
                      }}
                    ></div>
                    <div
                      className="gradient-segment team-b"
                      style={{
                        width: `${
                          (totalFavoredB / (totalFavoredA + totalFavoredB)) *
                          100
                        }%`,
                        backgroundColor: normBColor,
                      }}
                    ></div>
                  </div>
                )}
              </div>
            </button>
            <button
              className={`main-tab ${activeTab === "metrics" ? "active" : ""}`}
              onClick={() => setActiveTab("metrics")}
              style={getMainTabStyling("metrics", activeTab === "metrics")}
            >
              <div className="main-tab-content">
                <div className="tab-text">Metrics ({metricsFavoredA}:{metricsFavoredB})</div>
                {(metricsFavoredA > 0 || metricsFavoredB > 0) && (
                  <div className="tab-gradient-bar">
                    <div 
                      className="gradient-segment team-a"
                      style={{
                        width: `${(metricsFavoredA / (metricsFavoredA + metricsFavoredB)) * 100}%`,
                        backgroundColor: normAColor
                      }}
                    ></div>
                    <div 
                      className="gradient-segment team-b"
                      style={{
                        width: `${(metricsFavoredB / (metricsFavoredA + metricsFavoredB)) * 100}%`,
                        backgroundColor: normBColor
                      }}
                    ></div>
                  </div>
                )}
              </div>
            </button>
          </div>

          {/* Tab content */}
          <div className="tab-content">
            {activeTab === "statistics" && renderStatisticsTab()}
            {activeTab === "metrics" && renderMetricsTab()}
          </div>
        </div>

        <div className="team-comparison-footer">
          <button className="close-btn" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
