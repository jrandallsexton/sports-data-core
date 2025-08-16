// src/components/picks/LeagueWeekSelector.jsx
import "./LeagueWeekSelector.css";

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
      <div>
        <label htmlFor="leagueSelect">League:</label>
        <select
          id="leagueSelect"
          value={selectedLeagueId || ""}
          onChange={(e) => setSelectedLeagueId(e.target.value)}
        >
          {leagues.map((league) => (
            <option key={league.id} value={league.id}>
              {league.name}
            </option>
          ))}
        </select>
      </div>

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
