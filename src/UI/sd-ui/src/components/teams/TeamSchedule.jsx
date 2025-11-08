import { Link } from "react-router-dom";
import { formatToEasternTime } from "../../utils/timeUtils";
import "./TeamSchedule.css";

function TeamSchedule({ schedule, seasonYear }) {
  // Helper function to format game result
  const formatGameResult = (game) => {
    // If game is not finalized, show TBD
    if (!game.finalizedUtc) {
      return "TBD";
    }
    
    // Format the score and determine win/loss
    const score = `${game.awayScore}-${game.homeScore}`;
    const resultText = game.wasWinner ? "W" : "L";
    
    return `${resultText} | ${score}`;
  };

  // Helper function to get CSS class for result
  const getResultClass = (game) => {
    if (!game.finalizedUtc) {
      return "result-tbd";
    }
    return game.wasWinner ? "result-win" : "result-loss";
  };

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
                <td className={getResultClass(game)}>
                  {game.contestId ? (
                    <Link
                      to={`/app/sport/football/ncaa/contest/${game.contestId}`}
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
