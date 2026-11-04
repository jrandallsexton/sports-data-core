import React, { useEffect, useState } from "react";
import { useTheme } from "../../contexts/ThemeContext";
import apiWrapper from "../../api/apiWrapper";
import CFPBracket from "./CFPBracket";
import { teamLink } from '../../utils/sportLinks';
import useCurrentSeasonYear from "../../hooks/useCurrentSeasonYear";
import "./RankingsWidget.css";

function RankingsWidget({
  sport = "football",
  league = "ncaa",
  seasonYear: seasonYearProp,
  week,
}) {
  const { theme } = useTheme();
  // Season comes from the route when given, otherwise /seasons/current —
  // never a literal. The 2025-era hardcode froze this widget to a
  // finished season at rollover.
  const { seasonYear: currentSeasonYear, loading: currentSeasonLoading } =
    useCurrentSeasonYear(sport, league);
  const seasonYear = seasonYearProp ?? currentSeasonYear;
  const seasonLoading = seasonYearProp ? false : currentSeasonLoading;
  const [pollsData, setPollsData] = useState(null);
  const [activeTabIndex, setActiveTabIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (seasonLoading) return;
    if (seasonYear === null) {
      setPollsData([]);
      setLoading(false);
      return;
    }

    let cancelled = false;
    async function fetchRankings() {
      setLoading(true);
      setError(null);
      try {
        const apiResult = week
          ? await apiWrapper.Rankings.getCurrentRankings(seasonYear, week)
          : await apiWrapper.Rankings.getSeasonRankings(seasonYear, sport, league);
        if (cancelled) return;
        const data = apiResult?.data || apiResult;
        const polls = Array.isArray(data) ? data : [];
        setPollsData(polls);
        setActiveTabIndex(0);
      } catch (err) {
        if (!cancelled) setError("Failed to load rankings");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    fetchRankings();
    return () => {
      cancelled = true;
    };
    // sport/league in the deps: a route change between scopes with the
    // same resolved season must refetch, not retain the prior scope's polls.
  }, [seasonLoading, seasonYear, week, sport, league]);

  const activePoll = pollsData && pollsData.length > activeTabIndex ? pollsData[activeTabIndex] : null;

  // Generate tab label from poll data
  const getTabLabel = (poll) => {
    return poll.pollName;
  };

  return (
    <div className="rankings-widget">
      <h2>College Football Rankings</h2>
      {loading || seasonLoading ? (
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
            <div className={`rankings-content ${activePoll.bracket ? 'cfp-layout' : ''}`}>
              {/* Rankings Table */}
              <div className="rankings-table-container">
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
                      {activePoll.hasTrends && <th>&Delta;</th>}
                      </tr>
                    </thead>
                    <tbody>
                      {activePoll.entries
                        .slice(start, start + (idx === 0 ? 13 : 12))
                        .map((team, i) => (
                          <tr key={team.franchiseSeasonId || i}>
                            <td>{team.rank}</td>
                            <td>
                              {(() => {
                                const logoSrc = theme === 'dark'
                                  ? (team.franchiseLogoUrlDark || team.franchiseLogoUrlLight || team.franchiseLogoUrl)
                                  : (team.franchiseLogoUrlLight || team.franchiseLogoUrlDark || team.franchiseLogoUrl);
                                return logoSrc ? (
                                <img
                                  src={logoSrc}
                                  alt={team.franchiseName || "Logo"}
                                  style={{
                                    width: 20,
                                    height: 20,
                                    objectFit: "contain",
                                    marginRight: 6,
                                    verticalAlign: "middle",
                                  }}
                                />
                                ) : null;
                              })()}
                              <a
                                href={teamLink(team.franchiseSlug || '', seasonYear, sport, league)}
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
                          {activePoll.hasTrends && <td>{team.trend}</td>}
                          </tr>
                        ))}
                    </tbody>
                  </table>
                ))}
              </div>

              {/* CFP bracket renders only from real bracket data. The old
                  mock seeded ranks 1-12 straight through (not CFP seeding
                  rules) with a hard-coded championship site/date, so a
                  ranked list alone never draws one. */}
              {activePoll.bracket && (
                <div className="cfp-bracket-container">
                  <CFPBracket bracket={activePoll.bracket} />
                </div>
              )}
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
