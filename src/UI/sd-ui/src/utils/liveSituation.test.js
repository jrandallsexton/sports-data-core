import { describe, it, expect } from 'vitest';
import {
  formatBallSpot,
  formatDownAndDistance,
  formatFootballSituation,
  getPossessionGlyph,
  getPossessionSide,
} from './liveSituation';

const baseMatchup = {
  status: 'STATUS_IN_PROGRESS',
  awayShort: 'CAR',
  homeShort: 'JAX',
  awayFranchiseSeasonId: 'away-fs',
  homeFranchiseSeasonId: 'home-fs',
};

const withLive = (overrides) => ({ ...baseMatchup, ...overrides });

describe('formatDownAndDistance', () => {
  it.each([
    [1, 10, '1st & 10'],
    [2, 7, '2nd & 7'],
    [3, 1, '3rd & 1'],
    [4, 25, '4th & 25'],
  ])('formats down %i and %i to go', (down, distance, expected) => {
    expect(formatDownAndDistance(down, distance)).toBe(expected);
  });

  it('renders goal-to-go rather than "& 0"', () => {
    expect(formatDownAndDistance(1, 0)).toBe('1st & Goal');
  });

  it('returns null when there is no snap state', () => {
    // Down 0 is ESPN's kickoff / extra point / end-of-period value.
    expect(formatDownAndDistance(0, 0)).toBeNull();
    expect(formatDownAndDistance(null, 10)).toBeNull();
    expect(formatDownAndDistance(undefined, undefined)).toBeNull();
  });

  it('keeps the down when distance is unknown', () => {
    expect(formatDownAndDistance(2, null)).toBe('2nd');
  });
});

describe('formatBallSpot', () => {
  // Convention verified against stored play text: absolute from the HOME
  // goal line. With JAX at home, "to JAX 27" stores 27; "to CAR 27"
  // stores 73.
  it('names the home side below midfield and reads the number directly', () => {
    expect(formatBallSpot(27, 'CAR', 'JAX')).toBe('JAX 27');
  });

  it('names the away side above midfield and inverts the number', () => {
    expect(formatBallSpot(73, 'CAR', 'JAX')).toBe('CAR 27');
  });

  it('renders midfield as a bare 50', () => {
    expect(formatBallSpot(50, 'CAR', 'JAX')).toBe('50');
  });

  it('returns null for unknown or out-of-range positions', () => {
    expect(formatBallSpot(null, 'CAR', 'JAX')).toBeNull();
    expect(formatBallSpot(-1, 'CAR', 'JAX')).toBeNull();
    expect(formatBallSpot(101, 'CAR', 'JAX')).toBeNull();
  });
});

describe('formatFootballSituation', () => {
  it('joins down-and-distance with the ball spot', () => {
    // The real replayed case: home team driving, ball at the AWAY 25
    // stored as 75. Must read "CAR 25", not "JAX 25".
    expect(
      formatFootballSituation(withLive({ down: 1, distance: 10, ballOnYardLine: 75 }))
    ).toBe('1st & 10 · CAR 25');
  });

  it('renders either half alone', () => {
    expect(
      formatFootballSituation(withLive({ down: 3, distance: 4, ballOnYardLine: null }))
    ).toBe('3rd & 4');
    expect(
      formatFootballSituation(withLive({ down: 0, distance: 0, ballOnYardLine: 35 }))
    ).toBe('JAX 35');
  });

  it('returns null when neither half is known so no empty row renders', () => {
    expect(
      formatFootballSituation(withLive({ down: null, distance: null, ballOnYardLine: null }))
    ).toBeNull();
  });
});

describe('getPossessionSide', () => {
  it('resolves football possession by franchise season id', () => {
    expect(
      getPossessionSide(withLive({ possessionFranchiseSeasonId: 'home-fs' }), 'FootballNfl')
    ).toBe('home');
    expect(
      getPossessionSide(withLive({ possessionFranchiseSeasonId: 'away-fs' }), 'FootballNfl')
    ).toBe('away');
  });

  it('falls back to the last play team when baseball has no half inning', () => {
    // halfInning is SignalR-only, so a cold start has none; the REST
    // payload's possession (the last play's team) IS the batting side.
    expect(
      getPossessionSide(
        withLive({ halfInning: null, possessionFranchiseSeasonId: 'home-fs' }),
        'BaseballMlb'
      )
    ).toBe('home');
  });

  it('resolves baseball batting from the half inning', () => {
    expect(getPossessionSide(withLive({ halfInning: 'Top' }), 'BaseballMlb')).toBe('away');
    expect(getPossessionSide(withLive({ halfInning: 'Bottom' }), 'BaseballMlb')).toBe('home');
  });

  it('shows nothing once the game is final', () => {
    expect(
      getPossessionSide(
        withLive({ status: 'STATUS_FINAL', possessionFranchiseSeasonId: 'home-fs' }),
        'FootballNfl'
      )
    ).toBeNull();
  });

  it('still shows possession while a live game is paused', () => {
    expect(
      getPossessionSide(
        withLive({ status: 'STATUS_DELAYED', possessionFranchiseSeasonId: 'away-fs' }),
        'FootballNfl'
      )
    ).toBe('away');
  });

  it('does not match a null possession against a null franchise id', () => {
    // Both null would pass a bare === check and falsely mark possession.
    expect(
      getPossessionSide(
        {
          ...baseMatchup,
          possessionFranchiseSeasonId: null,
          awayFranchiseSeasonId: null,
        },
        'FootballNfl'
      )
    ).toBeNull();
  });
});

describe('getPossessionGlyph', () => {
  it('uses a baseball for MLB and a football everywhere else', () => {
    expect(getPossessionGlyph('BaseballMlb')).toBe('⚾');
    expect(getPossessionGlyph('FootballNcaa')).toBe('🏈');
    expect(getPossessionGlyph(null)).toBe('🏈');
  });
});
