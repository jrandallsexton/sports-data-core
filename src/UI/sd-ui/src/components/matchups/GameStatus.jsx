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
 * @param {string} props.period - Current period (Q1, Q2, Q3, Q4, OT, etc.)
 * @param {string} props.clock - Game clock time
 * @param {string} props.awayFranchiseSeasonId - Away team franchise season ID
 * @param {string} props.homeFranchiseSeasonId - Home team franchise season ID
 * @param {string} props.possessionFranchiseSeasonId - Team with possession ID
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
  location,
  period,
  clock,
  awayFranchiseSeasonId,
  homeFranchiseSeasonId,
  possessionFranchiseSeasonId
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
    const awayHasPossession = possessionFranchiseSeasonId === awayFranchiseSeasonId;
    const homeHasPossession = possessionFranchiseSeasonId === homeFranchiseSeasonId;

    return (
      <div className="game-result">
        <div className="final-score">
          <span className="result-label live-indicator">LIVE</span>
          {period && clock && (
            <span className="game-clock">{period} - {clock}</span>
          )}
          <span className="score-display">
            {awayHasPossession && <span className="possession-indicator">🏈</span>}
            {awayShort} {awayScore} - {homeScore} {homeShort}
            {homeHasPossession && <span className="possession-indicator">🏈</span>}
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
