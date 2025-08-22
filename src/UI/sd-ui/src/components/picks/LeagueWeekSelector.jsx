// src/components/picks/LeagueWeekSelector.jsx
import "./LeagueWeekSelector.css";
import LeagueSelector from "../shared/LeagueSelector";

function LeagueWeekSelector({
  leagues = [],
  selectedLeagueId,
  setSelectedLeagueId,
  selectedWeek,
  setSelectedWeek,
}) {
  return (
    <div className="league-week-selector">
      {/* League Select */}
      <LeagueSelector
        leagues={leagues}
        selectedLeagueId={selectedLeagueId}
        setSelectedLeagueId={setSelectedLeagueId}
      />

      {/* Week Select */}
      <div>
        <label htmlFor="weekSelect">Week:</label>
        <select
          id="weekSelect"
          value={selectedWeek}
          onChange={(e) => setSelectedWeek(Number(e.target.value))}
        >
          {Array.from({ length: 12 }, (_, i) => (
            <option key={i + 1} value={i + 1}>
              Week {i + 1}
            </option>
          ))}
        </select>
      </div>
    </div>
  );
}

export default LeagueWeekSelector;
