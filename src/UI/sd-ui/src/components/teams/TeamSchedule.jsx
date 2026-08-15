// src/components/teams/TeamSchedule.jsx
import { Fragment } from "react";
import { Link } from "react-router-dom";
import { formatToUserTime, getZoneAbbreviation, getStartLabel, getDefaultGameWeekday } from "../../utils/timeUtils";
import { useUserTimeZone } from "../../hooks/useUserTimeZone";
import { teamLink, contestLink } from '../../utils/sportLinks';
import "./TeamSchedule.css";

function TeamSchedule({ schedule, seasonYear, sport = 'football', league = 'ncaa' }) {
  const userTz = useUserTimeZone();
  const startLabel = getStartLabel(sport);

  // Per-game kickoff label: the calendar/time plus the zone abbreviation for
  // THAT date. A single season-wide header abbreviation is wrong — a season
  // spans the DST switch, so an August game is EDT and a December game EST.
  // Midnight ("TBD") carries no meaningful clock zone, so the abbrev is
  // suppressed there.
  const kickoffLabel = (game) => {
    const time = formatToUserTime(game.date, userTz, getDefaultGameWeekday(`${sport}-${league}`));
    if (time.endsWith("TBD")) return time;
    return `${time} ${getZoneAbbreviation(userTz, game.date)}`;
  };
  // Helper function to format game result
  const formatGameResult = (game) => {
    if (game.status === "STATUS_FINAL") {
      const score = `${game.awayScore}-${game.homeScore}`;
      const resultText = game.wasWinner ? "W" : "L";
      return `${resultText} | ${score}`;
    }
    // statusDescription is the human-readable form ("In Progress", "Postponed",
    // "Rain Delay"). Fall back to "TBD" when the description is missing
    // (e.g. games with no CompetitionStatus row yet).
    return game.statusDescription || "TBD";
  };

  // Helper function to get CSS class for result
  const getResultClass = (game) => {
    if (game.status === "STATUS_FINAL") {
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
            {/* No zone abbreviation here — it's per-row, because the season
                crosses the DST boundary (see kickoffLabel). */}
            <th>{startLabel}</th>
            <th>Opponent</th>
            <th>Location</th>
            <th>Result</th>
          </tr>
        </thead>
        <tbody>
          {schedule?.length ? (
            schedule.map((game, idx) => {
              // Schedule is date-ordered and phases are chronologically
              // contiguous (Preseason -> Regular Season -> Postseason), so a
              // phase-changed check emits one section header per phase.
              const prevPhase = idx > 0 ? schedule[idx - 1].seasonPhase : null;
              const showPhaseHeader =
                game.seasonPhase && game.seasonPhase !== prevPhase;
              return (
                <Fragment key={game.contestId ?? idx}>
                  {showPhaseHeader && (
                    <tr className="schedule-phase-row">
                      <th colSpan="4" scope="colgroup">{game.seasonPhase}</th>
                    </tr>
                  )}
                  <tr>
                    <td>{kickoffLabel(game)}</td>
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
                </Fragment>
              );
            })
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
