import "./LeagueWeekSelector.css";
import LeagueSelector from "../shared/LeagueSelector";

function LeagueWeekSelector({
  leagues = [],
  selectedLeagueId,
  setSelectedLeagueId,
  selectedWeek,
  setSelectedWeek,
  maxSeasonWeek = 1,
  allowAll = false, // New prop to enable "All" option
}) {
  return (
    <div className="league-week-selector">
      {/* League Select */}
      <div className="selector-block">
        <LeagueSelector
          leagues={leagues}
          selectedLeagueId={selectedLeagueId}
          setSelectedLeagueId={setSelectedLeagueId}
          allowAll={allowAll}
        />
      </div>

      {/* Week Select */}
      <div className="selector-block">
        <label htmlFor="weekSelect">Week:</label>
        <select
          id="weekSelect"
          value={selectedWeek ?? ""}
          onChange={(e) => setSelectedWeek(e.target.value ? Number(e.target.value) : null)}
          disabled={!maxSeasonWeek}
        >
          {allowAll && <option value="">All Weeks</option>}
          {maxSeasonWeek && Array.from({ length: maxSeasonWeek }, (_, i) => (
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
