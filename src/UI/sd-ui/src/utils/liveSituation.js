/**
 * Live-game situation helpers shared by the team rows (possession glyph)
 * and the status block (situation line), so the two surfaces can never
 * disagree about who has the ball.
 *
 * Mobile parity: sd-mobile/src/utils/liveSituation.ts — keep the two in
 * step; the formatting rules and the yard-line convention below are the
 * shared contract.
 */

/** Statuses where a possession / batting indicator is meaningful: the
 *  actively-live states plus the paused-live ones (a delayed game still
 *  has a team with the ball). FINAL is deliberately excluded — the last
 *  play's possession lingers in the live context, and showing a football
 *  on a finished game is simply wrong. */
const LIVE_STATUSES = new Set([
  'STATUS_IN_PROGRESS',
  'STATUS_HALFTIME',
  'STATUS_DELAYED',
  'STATUS_RAIN_DELAY',
  'STATUS_SUSPENDED',
]);

export function isLiveStatus(status) {
  return !!status && LIVE_STATUSES.has(status);
}

/**
 * Which side currently has the ball (football) or is batting (baseball).
 * Football reads possessionFranchiseSeasonId; baseball derives it from
 * halfInning ("Top" → away bats, "Bottom" → home bats). Returns null when
 * the game isn't live or the state is unknown.
 */
export function getPossessionSide(matchup, leagueSport) {
  if (!matchup || !isLiveStatus(matchup.status)) return null;

  if (leagueSport === 'BaseballMlb') {
    const half = (matchup.halfInning ?? '').toLowerCase();
    if (half === 'top') return 'away';
    if (half === 'bottom') return 'home';
    // halfInning rides on the SignalR play event and is NOT stored, so a
    // cold start has none. The REST payload does carry the last play's
    // team, which for baseball IS the batting side — fall through to the
    // shared possession check rather than showing nothing.
  }

  const possessionId = matchup.possessionFranchiseSeasonId;
  if (possessionId == null) return null;
  if (possessionId === matchup.awayFranchiseSeasonId) return 'away';
  if (possessionId === matchup.homeFranchiseSeasonId) return 'home';
  return null;
}

/** Glyph for the possession indicator, by sport. */
export function getPossessionGlyph(leagueSport) {
  return leagueSport === 'BaseballMlb' ? '⚾' : '🏈';
}

/**
 * "2nd & 7" — null when there is no snap state. Down 0 covers kickoffs,
 * extra points, and end-of-period, where ESPN reports down 0 and a
 * "0th & 0" line would be nonsense. Distance 0 on a valid down is
 * goal-to-go (1st & Goal from the 3).
 */
export function formatDownAndDistance(down, distance) {
  if (down == null || down <= 0) return null;

  const ordinals = ['1st', '2nd', '3rd', '4th'];
  const downLabel = ordinals[down - 1] ?? `${down}th`;

  if (distance == null) return downLabel;
  if (distance <= 0) return `${downLabel} & Goal`;
  return `${downLabel} & ${distance}`;
}

/**
 * "LV 25" — the ball spot in the conventional team-relative form.
 *
 * ballOnYardLine is an ABSOLUTE field coordinate measured from the HOME
 * team's goal line (home goal = 0, away goal = 100). Verified against
 * stored play text across several games and both orientations: with PHI
 * at home, "to PHI 32" stores 32; with PHI away, "to PHI 6" stores 94.
 * So a spot below 50 is in HOME territory and reads directly, and one
 * above 50 is in AWAY territory and reads inverted. Midfield is just
 * "50". Needs no possession knowledge — which side of the field the ball
 * sits on is what names the spot.
 */
export function formatBallSpot(ballOnYardLine, awayShort, homeShort) {
  if (ballOnYardLine == null) return null;
  if (ballOnYardLine < 0 || ballOnYardLine > 100) return null;
  if (ballOnYardLine === 50) return '50';

  if (ballOnYardLine < 50) {
    return homeShort ? `${homeShort} ${ballOnYardLine}` : null;
  }
  return awayShort ? `${awayShort} ${100 - ballOnYardLine}` : null;
}

/**
 * The football situation line: "2nd & 7 · LV 25". Either half can be
 * missing — a spot with no down still tells you where the ball is, and a
 * down with no spot still tells you the snap. Null when neither exists,
 * so the caller renders nothing rather than an empty row.
 */
export function formatFootballSituation({ down, distance, ballOnYardLine, awayShort, homeShort }) {
  const downAndDistance = formatDownAndDistance(down, distance);
  const spot = formatBallSpot(ballOnYardLine, awayShort, homeShort);

  const parts = [downAndDistance, spot].filter(Boolean);
  return parts.length > 0 ? parts.join(' · ') : null;
}
