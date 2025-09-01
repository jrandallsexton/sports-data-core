import "./LeagueSelector.css";

function LeagueSelector({ leagues = [], selectedLeagueId, setSelectedLeagueId }) {
  const handleLeagueChange = (e) => {
    const newLeagueId = e.target.value;
    setSelectedLeagueId(newLeagueId);
  };

  return (
    <div className="league-selector">
      <label htmlFor="leagueSelect">League:</label>
      <select
        id="leagueSelect"
        value={selectedLeagueId || ""}
        onChange={handleLeagueChange}
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
