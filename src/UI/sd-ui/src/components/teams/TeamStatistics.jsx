import React, { useState } from "react";
import "./TeamStatistics.css";

function TeamStatistics({ team, seasonYear, stats }) {
  const statistics = stats?.data?.statistics || stats?.statistics;
  const categories = statistics ? Object.keys(statistics) : [];
  const [selectedCategory, setSelectedCategory] = useState(categories[0] || "");

  if (!statistics) {
    return <div>No statistics available.</div>;
  }

  return (
    <div className="team-statistics">
      <h3>Team Statistics ({seasonYear})</h3>
      <div className="team-statistics-tabs">
        {categories.map((cat) => (
          <button
            key={cat}
            className={cat === selectedCategory ? "active" : ""}
            onClick={() => setSelectedCategory(cat)}
          >
            {statistics[cat]?.[0]?.categoryDisplayName ?? cat.charAt(0).toUpperCase() + cat.slice(1)}
          </button>
        ))}
      </div>
      {selectedCategory && (
        <div className="stat-category-section">
          {/* <h4 className="stat-category-title">{selectedCategory.charAt(0).toUpperCase() + selectedCategory.slice(1)}</h4> */}
          <table className="team-statistics-table">
            <thead>
              <tr>
                <th>Statistic</th>
                <th>Value</th>
                <th>Per Game</th>
                <th>Rank</th>
              </tr>
            </thead>
            <tbody>
              {statistics[selectedCategory].map((entry, idx) => (
                <tr key={entry.statisticKey ?? idx}>
                  {/* The wire's human-readable name is statisticValue
                      (StatFormattingService writes the friendly label there;
                      statisticKey is the raw ESPN key). Same read as mobile's
                      StatsComparisonModal. `statistic` never existed on this
                      DTO after the 2025-10 rename, so this column was blank. */}
                  <td>{entry.statisticValue ?? entry.statisticKey ?? "-"}</td>
                  <td>{entry.displayValue ?? "-"}</td>
                  <td>{entry.perGameDisplayValue ?? entry.perGameValue ?? "-"}</td>
                  <td>{entry.rank ?? "-"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default TeamStatistics;
