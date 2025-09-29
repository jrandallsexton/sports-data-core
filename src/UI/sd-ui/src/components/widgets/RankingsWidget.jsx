import React, { useEffect, useState } from "react";
import apiWrapper from "../../api/apiWrapper";
import "./RankingsWidget.css";

function RankingsWidget() {
  const [poll, setPoll] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    async function fetchRankings() {
      setLoading(true);
      setError(null);
      try {
        const apiResult = await apiWrapper.Rankings.getCurrentRankings(2025, 6);
        setPoll(apiResult?.data || apiResult);
      } catch (err) {
        setError("Failed to load rankings");
      } finally {
        setLoading(false);
      }
    }
    fetchRankings();
  }, []);

  return (
    <div className="rankings-widget">
      <h2>AP Top 25 - Week 6</h2>
      {loading ? (
        <div>Loading rankings...</div>
      ) : error ? (
        <div className="error">{error}</div>
      ) : poll && poll.entries ? (
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
              style={{ width: 340, minWidth: 240 }}
            >
              <thead>
                <tr>
                  <th>Rank</th>
                  <th>Team</th>
                  <th>Record</th>
                  <th>Points</th>
                  <th>1st</th>
                  <th>Trend</th>
                </tr>
              </thead>
              <tbody>
                {poll.entries
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
                      <td>{team.points}</td>
                      <td>
                        {team.firstPlaceVotes > 0
                          ? team.firstPlaceVotes
                          : ""}
                      </td>
                      <td>{team.trend}</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          ))}
        </div>
      ) : (
        <div>No rankings available.</div>
      )}
    </div>
  );
}

export default RankingsWidget;
