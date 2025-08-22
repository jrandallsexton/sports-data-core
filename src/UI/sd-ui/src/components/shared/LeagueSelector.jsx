import "./LeagueSelector.css";

function LeagueSelector({ leagues = [], selectedLeagueId, setSelectedLeagueId }) {
  return (
    <div className="league-selector">
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
  );
}

export default LeagueSelector;
