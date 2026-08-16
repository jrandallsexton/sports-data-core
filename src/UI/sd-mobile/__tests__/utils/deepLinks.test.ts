import {
  getLeagueInviteId,
  getMatchupTarget,
  toPendingDeepLink,
} from '@/src/utils/deepLinks';

// FCM data payloads are string maps on the wire — every fixture below uses
// strings, including week, so the parsing under test is the real thing.

describe('getLeagueInviteId', () => {
  it('returns the leagueId for a LeagueInvite payload', () => {
    expect(
      getLeagueInviteId({ kind: 'LeagueInvite', leagueId: 'abc' }),
    ).toBe('abc');
  });

  it('returns null for other kinds and for missing data', () => {
    expect(getLeagueInviteId({ kind: 'OddsChanged', leagueId: 'abc' })).toBeNull();
    expect(getLeagueInviteId(null)).toBeNull();
    expect(getLeagueInviteId({ kind: 'LeagueInvite' })).toBeNull();
  });
});

describe('getMatchupTarget', () => {
  const base = {
    kind: 'OddsChanged',
    target: 'matchup',
    contestId: 'contest-1',
    sport: 'FootballNcaa',
  };

  it('maps the Sport enum to route segments', () => {
    expect(getMatchupTarget(base)).toEqual({
      sport: 'football',
      league: 'ncaa',
      contestId: 'contest-1',
      leagueId: undefined,
      week: undefined,
    });
  });

  it('carries leagueId and parses week from its wire string', () => {
    const result = getMatchupTarget({ ...base, leagueId: 'lg-1', week: '3' });
    expect(result?.leagueId).toBe('lg-1');
    expect(result?.week).toBe(3);
  });

  it('drops an unparseable week rather than passing NaN downstream', () => {
    expect(getMatchupTarget({ ...base, week: 'soon' })?.week).toBeUndefined();
  });

  it.each([
    ['a numeric prefix', '3invalid'],
    ['a decimal', '3.5'],
    ['a negative', '-3'],
    ['whitespace only', '   '],
    ['an empty string', ''],
    ['a precision-losing digit string', '999999999999999999999'],
  ])('rejects %s rather than routing to a wrong week', (_label, week) => {
    // parseInt would take the valid PREFIX of "3invalid" and "3.5", yielding 3
    // — a plausible-looking wrong week is worse than no week at all, since
    // gameRoute treats week as optional and degrades cleanly without it.
    expect(getMatchupTarget({ ...base, week })?.week).toBeUndefined();
  });

  it('still accepts a clean integer week', () => {
    expect(getMatchupTarget({ ...base, week: '12' })?.week).toBe(12);
  });

  it('accepts PickScored, the other matchup-bound kind', () => {
    // Both line-move and pick-scored pushes land on the game page; the
    // server-side twin is MatchupDeepLink's *Kind constants.
    const result = getMatchupTarget({ ...base, kind: 'PickScored', leagueId: 'lg-9', week: '5' });
    expect(result).toEqual({
      sport: 'football',
      league: 'ncaa',
      contestId: 'contest-1',
      leagueId: 'lg-9',
      week: 5,
    });
  });

  it('returns null for an unknown sport enum', () => {
    // resolveSportLeague is deliberately strict; an unsupported sport must
    // not route to a wrong-sport screen.
    expect(getMatchupTarget({ ...base, sport: 'CricketIpl' })).toBeNull();
  });

  it('returns null when required fields are missing or the kind differs', () => {
    expect(getMatchupTarget({ ...base, contestId: undefined })).toBeNull();
    expect(getMatchupTarget({ ...base, kind: 'LeagueInvite' })).toBeNull();
    expect(getMatchupTarget(null)).toBeNull();
  });
});

describe('toPendingDeepLink', () => {
  it('discriminates invite payloads', () => {
    expect(toPendingDeepLink({ kind: 'LeagueInvite', leagueId: 'lg' })).toEqual({
      kind: 'invite',
      leagueId: 'lg',
    });
  });

  it('discriminates matchup payloads', () => {
    expect(
      toPendingDeepLink({
        kind: 'OddsChanged',
        contestId: 'c1',
        sport: 'FootballNfl',
        leagueId: 'lg',
        week: '2',
      }),
    ).toEqual({
      kind: 'matchup',
      sport: 'football',
      league: 'nfl',
      contestId: 'c1',
      leagueId: 'lg',
      week: 2,
    });
  });

  it('returns null for an unrecognized payload so the tap is a no-op', () => {
    expect(toPendingDeepLink({ kind: 'SomethingElse' })).toBeNull();
    expect(toPendingDeepLink(undefined)).toBeNull();
  });
});
