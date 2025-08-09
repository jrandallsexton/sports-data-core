import { Link } from "react-router-dom";
import { formatToEasternTime } from "../../utils/timeUtils";
import "./TeamSchedule.css";

function TeamSchedule({ schedule, seasonYear }) {
  return (
    <div className="team-schedule">
      <h3>Schedule ({seasonYear})</h3>
      <table>
        <thead>
          <tr>
            <th>Kickoff (ET)</th>
            <th>Opponent</th>
            <th>Location</th>
            <th>Result</th>
          </tr>
        </thead>
        <tbody>
          {schedule?.length ? (
            schedule.map((game, idx) => (
              <tr key={idx}>
                <td>{formatToEasternTime(game.date)}</td>
                <td>
                  <Link
                    to={`/app/sport/football/ncaa/team/${game.opponentSlug}/${seasonYear}`}
                    className="team-link"
                  >
                    {game.opponent}
                  </Link>
                </td>
                <td>{game.location}</td>
                <td
                  className={
                    game.result?.trim().toUpperCase().startsWith("W")
                      ? "result-win"
                      : "result-loss"
                  }
                >
                  {game.result}
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
