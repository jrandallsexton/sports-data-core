import React, { useState } from 'react';
import LeaderCard from './LeaderCard';
import './LeadersPanel.css';

// House convention: a season is labeled by its starting year, and the
// rollover happens in June (month >= 6). Same rule as FranchiseMetricsGrid.
const getCurrentSeasonYear = () => {
  const now = new Date();
  return now.getMonth() + 1 >= 6 ? now.getFullYear() : now.getFullYear() - 1;
};

// FBS athlete season stats exist from 2004 (2025 is thinner than the rest).
const EARLIEST_SEASON = 2004;

const seasonYears = [];
for (let y = getCurrentSeasonYear(); y >= EARLIEST_SEASON; y--) {
  seasonYears.push(y);
}

// A widget is config: ESPN category + stat names, confirmed against prod data.
export const LEADER_WIDGETS = [
  { title: 'Passing Yards', category: 'passing', stat: 'passingYards' },
  { title: 'Rushing Yards', category: 'rushing', stat: 'rushingYards' },
  { title: 'Receiving Yards', category: 'receiving', stat: 'receivingYards' },
  { title: 'Sacks', category: 'defensive', stat: 'sacks' },
  { title: 'Tackles for Loss', category: 'defensive', stat: 'tacklesForLoss' },
  { title: 'Interceptions', category: 'defensiveInterceptions', stat: 'interceptions' },
];

function LeadersPanel() {
  const [seasonYear, setSeasonYear] = useState(seasonYears[0]);

  return (
    <div className="leaders-panel">
      <div className="leaders-panel-header">
        <h2>Season Leaders ({seasonYear})</h2>
        <select
          value={seasonYear}
          onChange={(e) => setSeasonYear(Number(e.target.value))}
          className="leaders-season-select"
          aria-label="Leaders season"
        >
          {seasonYears.map((year) => (
            <option key={year} value={year}>
              {year}
            </option>
          ))}
        </select>
      </div>

      <div className="leaders-panel-grid">
        {LEADER_WIDGETS.map((widget) => (
          <LeaderCard
            key={`${widget.category}.${widget.stat}`}
            title={widget.title}
            category={widget.category}
            stat={widget.stat}
            seasonYear={seasonYear}
          />
        ))}
      </div>
    </div>
  );
}

export default LeadersPanel;
