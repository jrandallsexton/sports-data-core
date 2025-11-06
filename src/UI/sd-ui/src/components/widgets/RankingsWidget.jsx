import React, { useEffect, useState } from "react";
import apiWrapper from "../../api/apiWrapper";
import "./RankingsWidget.css";

function RankingsWidget() {
  const [pollsData, setPollsData] = useState(null);
  const [activeTabIndex, setActiveTabIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    async function fetchRankings() {
      setLoading(true);
      setError(null);
      try {
        const apiResult = await apiWrapper.Rankings.getCurrentRankings(2025, 11);
        const data = apiResult?.data || apiResult;
        setPollsData(Array.isArray(data) ? data : []);
        setActiveTabIndex(0);
      } catch (err) {
        setError("Failed to load rankings");
      } finally {
        setLoading(false);
      }
    }
    fetchRankings();
  }, []);

  const activePoll = pollsData && pollsData.length > activeTabIndex ? pollsData[activeTabIndex] : null;

  // Generate tab label from poll data
  const getTabLabel = (poll) => {
    const pollNames = {
      'cfp': 'CFP',
      'ap': 'AP Top 25',
      'coaches': 'Coaches Poll'
    };
    const name = pollNames[poll.pollName] || poll.pollName.toUpperCase();
    return `${name}`;
  };

  return (
    <div className="rankings-widget">
      <h2>College Football Rankings</h2>
      {loading ? (
        <div>Loading rankings...</div>
      ) : error ? (
        <div className="error">{error}</div>
      ) : pollsData && pollsData.length > 0 ? (
        <>
          {/* Tabs */}
          <div className="rankings-tabs">
            {pollsData.map((poll, index) => (
              <button
                key={index}
                className={`rankings-tab ${activeTabIndex === index ? "active" : ""}`}
                onClick={() => setActiveTabIndex(index)}
              >
                {getTabLabel(poll)}
              </button>
            ))}
          </div>

          {/* Active Poll Content */}
          {activePoll && activePoll.entries ? (
            <div
              style={{
                display: "flex",
                gap: "2rem",
                flexWrap: "wrap",
                justifyContent: "center",
              }}
            >
              {[0, 13].map((start, idx) => (
                <table
                  className="rankings-table"
                  key={idx}
                >
                  <thead>
                    <tr>
                      <th>
                        <span className="rank-header-desktop">Rank</span>
                        <span className="rank-header-mobile">Rk</span>
                      </th>
                      <th>Team</th>
                      <th>Record</th>
                      {activePoll.hasPoints && <th>Points</th>}
                      {activePoll.hasFirstPlaceVotes && <th>1st</th>}
                      <th>Trend</th>
                    </tr>
                  </thead>
                  <tbody>
                    {activePoll.entries
                      .slice(start, start + (idx === 0 ? 13 : 12))
                      .map((team, i) => (
                        <tr key={team.franchiseSeasonId || i}>
                          <td>{team.rank}</td>
                          <td>
                            {team.franchiseLogoUrl && (
                              <img
                                src={team.franchiseLogoUrl}
                                alt={team.franchiseName || "Logo"}
                                style={{
                                  width: 20,
                                  height: 20,
                                  objectFit: "contain",
                                  marginRight: 6,
                                  verticalAlign: "middle",
                                }}
                              />
                            )}
                            <a
                              href={`/app/sport/football/ncaa/team/${team.franchiseSlug || ''}/2025`}
                              className="team-link"
                              style={{ color: '#61dafb', textDecoration: 'underline', fontWeight: 500 }}
                            >
                              {team.franchiseName || "Unknown"}
                            </a>
                          </td>
                          <td>
                            {team.wins}-{team.losses}
                          </td>
                          {activePoll.hasPoints && <td>{team.points}</td>}
                          {activePoll.hasFirstPlaceVotes && (
                            <td>
                              {team.firstPlaceVotes > 0
                                ? team.firstPlaceVotes
                                : ""}
                            </td>
                          )}
                          <td>{team.trend}</td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              ))}
            </div>
          ) : (
            <div>No entries available for this poll.</div>
          )}
        </>
      ) : (
        <div>No rankings available.</div>
      )}
    </div>
  );
}

export default RankingsWidget;
