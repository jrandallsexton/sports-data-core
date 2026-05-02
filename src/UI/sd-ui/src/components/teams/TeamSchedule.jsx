// src/components/teams/TeamSchedule.jsx
import { Link } from "react-router-dom";
import { formatToUserTime, getZoneAbbreviation, getStartLabel } from "../../utils/timeUtils";
import { useUserTimeZone } from "../../hooks/useUserTimeZone";
import { teamLink, contestLink } from '../../utils/sportLinks';
import "./TeamSchedule.css";

function TeamSchedule({ schedule, seasonYear, sport = 'football', league = 'ncaa' }) {
  const userTz = useUserTimeZone();
  const zoneAbbrev = getZoneAbbreviation(userTz);
  const startLabel = getStartLabel(sport);
  // Helper function to format game result
  const formatGameResult = (game) => {
    if (game.status === "Final") {
      const score = `${game.awayScore}-${game.homeScore}`;
      const resultText = game.wasWinner ? "W" : "L";
      return `${resultText} | ${score}`;
    }
    return game.status || "TBD";
  };

  // Helper function to get CSS class for result
  const getResultClass = (game) => {
    if (game.status === "Final") {
      return game.wasWinner ? "result-win" : "result-loss";
    }
    return "result-tbd";
  };

  return (
    <div className="team-schedule">
      <h3>Schedule ({seasonYear})</h3>
      <table>
        <thead>
          <tr>
            <th>{startLabel} ({zoneAbbrev})</th>
            <th>Opponent</th>
            <th>Location</th>
            <th>Result</th>
          </tr>
        </thead>
        <tbody>
          {schedule?.length ? (
            schedule.map((game, idx) => (
              <tr key={idx}>
                <td>{formatToUserTime(game.date, userTz)}</td>
                <td>
                  <Link
                    to={teamLink(game.opponentSlug, seasonYear, sport, league)}
                    className="team-link"
                  >
                    {game.opponent}
                  </Link>
                </td>
                <td>{game.location}</td>
                <td className={getResultClass(game)}>
                  {game.contestId ? (
                    <Link
                      to={contestLink(game.contestId, sport, league)}
                      className="result-link"
                    >
                      {formatGameResult(game)}
                    </Link>
                  ) : (
                    formatGameResult(game)
                  )}
                </td>
              </tr>
            ))
          ) : (
            <tr>
              {/* 4 columns in header → 4 here */}
              <td colSpan="4">No games scheduled.</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

export default TeamSchedule;
