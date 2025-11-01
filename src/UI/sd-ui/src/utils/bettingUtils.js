/**
 * Calculate spread movement arrow indicator
 * @param {number} current - Current spread value
 * @param {number} open - Opening spread value
 * @returns {JSX.Element|null} Arrow indicator or null if no movement
 */
export const calculateSpreadArrow = (current, open) => {
  if (
    open === undefined ||
    open === null ||
    current === open
  ) {
    return null;
  }

  const absCurrent = Math.abs(Number(current));
  const absOpen = Math.abs(Number(open));

  if (absCurrent < absOpen) {
    // Spread moved closer to zero (easier for home) - green down arrow
    return <span style={{ color: '#00c853', fontWeight: 700, marginRight: 2 }} title="Spread moved in favor of home">▼</span>;
  } else if (absCurrent > absOpen) {
    // Spread moved further from zero (harder for home) - red up arrow
    return <span style={{ color: '#ff1744', fontWeight: 700, marginRight: 2 }} title="Spread moved against home">▲</span>;
  }

  return null;
};

/**
 * Calculate over/under movement arrow indicator
 * @param {number|string} current - Current O/U value
 * @param {number} open - Opening O/U value
 * @returns {JSX.Element|null} Arrow indicator or null if no movement
 */
export const calculateOverUnderArrow = (current, open) => {
  if (
    open === undefined ||
    open === null ||
    current === 'Off' ||
    current === open
  ) {
    return null;
  }

  if (Number(current) > Number(open)) {
    // O/U moved up (raised) - red
    return <span style={{ color: '#ff1744', fontWeight: 700, marginRight: 2 }} title="O/U moved up">▲</span>;
  } else if (Number(current) < Number(open)) {
    // O/U moved down (lowered) - green
    return <span style={{ color: '#00c853', fontWeight: 700, marginRight: 2 }} title="O/U moved down">▼</span>;
  }

  return null;
};

/**
 * Get CSS class for card border based on game status and pick result
 * @param {string} status - Game status ('Final', 'InProgress', 'Scheduled')
 * @param {object} userPickResult - User pick result object
 * @param {string} pickResult - Pick result ('correct', 'incorrect', or null)
 * @param {string} userPickFranchiseSeasonId - Legacy pick ID
 * @returns {string} CSS class name
 */
export const getPickResultClass = (status, userPickResult, pickResult, userPickFranchiseSeasonId) => {
  if (status !== 'Final') return ""; // No border for non-final games
  
  // Check if user made a pick (either in new or old format)
  if (!userPickResult && !userPickFranchiseSeasonId) return "pick-no-submission"; // Red border for no pick
  
  return pickResult ? `pick-${pickResult}` : ""; // Green/red based on result
};
