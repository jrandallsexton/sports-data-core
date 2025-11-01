/**
 * GameStatus component - displays game status (Final, Live, or Scheduled)
 * @param {object} props
 * @param {string} props.status - Game status ('Final', 'InProgress', 'Scheduled')
 * @param {string} props.awayShort - Away team short name
 * @param {string} props.homeShort - Home team short name
 * @param {number} props.awayScore - Away team score
 * @param {number} props.homeScore - Home team score
 * @param {string} props.gameTime - Formatted game time
 * @param {string} props.broadcasts - Broadcast information
 * @param {string} props.venue - Venue name
 * @param {string} props.location - Venue location
 */
function GameStatus({
  status,
  awayShort,
  homeShort,
  awayScore,
  homeScore,
  gameTime,
  broadcasts,
  venue,
  location
}) {
  if (status === 'Final') {
    return (
      <div className="game-result">
        <div className="final-score">
          <span className="result-label">FINAL:</span>
          <span className="score-display">
            {awayShort} {awayScore} - {homeScore} {homeShort}
          </span>
        </div>
      </div>
    );
  }

  if (status === 'InProgress') {
    return (
      <div className="game-result">
        <div className="final-score">
          <span className="result-label">LIVE:</span>
          <span className="score-display">
            {awayShort} {awayScore} - {homeScore} {homeShort}
          </span>
        </div>
      </div>
    );
  }

  // Scheduled or other status
  return (
    <div className="game-time-location">
      <div>{gameTime} | {broadcasts}</div>
      <div>{venue} | {location}</div>
    </div>
  );
}

export default GameStatus;
