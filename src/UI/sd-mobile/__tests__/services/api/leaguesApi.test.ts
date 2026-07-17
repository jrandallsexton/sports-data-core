import {
  leaguesApi,
  type CreateBaseballMlbLeagueRequest,
  type CreateFootballNcaaLeagueRequest,
  type CreateFootballNflLeagueRequest,
  type CreateLeagueResponse,
  type LeagueSummary,
} from '@/src/services/api/leaguesApi';

// Mock the shared axios instance — we only care that the right URL + body go out.
jest.mock('@/src/services/api/client', () => ({
  apiClient: {
    post: jest.fn(),
    get: jest.fn(),
  },
}));

// eslint-disable-next-line @typescript-eslint/no-require-imports
const { apiClient } = require('@/src/services/api/client');

// Shared payload scaffolding — every sport uses the same base fields,
// plus sport-specific additions (rankings + conferences for NCAA,
// divisions for NFL/MLB).
const basePayload = {
  name: 'Test League',
  description: null,
  pickType: 'StraightUp' as const,
  tiebreakerType: 'TotalPoints' as const,
  tiebreakerTiePolicy: 'EarliestSubmission' as const,
  useConfidencePoints: false,
  isPublic: false,
  dropLowWeeksCount: 0,
  startsOn: null,
  endsOn: null,
};

const ncaaPayload: CreateFootballNcaaLeagueRequest = {
  ...basePayload,
  rankingFilter: null,
  conferenceSlugs: [],
};

const nflPayload: CreateFootballNflLeagueRequest = {
  ...basePayload,
  divisionSlugs: [],
};

const mlbPayload: CreateBaseballMlbLeagueRequest = {
  ...basePayload,
  divisionSlugs: [],
};

/**
 * Generic test harness: each sport helper is passed in with its real type so
 * payload inference + return type flow through unchanged. If any helper's
 * signature drifts (e.g. a caller starts returning `Promise<unknown>` instead
 * of the real AxiosResponse), the call site here errors — which is the point
 * the previous `as (p: unknown) => Promise<{ data: unknown }>` casts
 * defeated. Structurally, `AxiosResponse<T>` satisfies `{ data: T }`, so
 * narrowing to `{ data: CreateLeagueResponse }` in the return type is the
 * minimum contract we actually assert.
 */
function testCreateLeague<T>(
  name: string,
  endpoint: string,
  method: (payload: T) => Promise<{ data: CreateLeagueResponse }>,
  payload: T,
) {
  describe(name, () => {
    beforeEach(() => {
      (apiClient.post as jest.Mock).mockReset();
      (apiClient.post as jest.Mock).mockResolvedValue({ data: { id: 'league-1' } });
    });

    it(`POSTs to ${endpoint} with the given payload`, async () => {
      await method(payload);

      expect(apiClient.post).toHaveBeenCalledTimes(1);
      expect(apiClient.post).toHaveBeenCalledWith(endpoint, payload);
    });

    it('returns the server response body', async () => {
      const response = await method(payload);
      expect(response.data).toEqual({ id: 'league-1' });
    });
  });
}

testCreateLeague(
  'leaguesApi.createFootballNcaaLeague',
  '/ui/leagues/football/ncaa',
  leaguesApi.createFootballNcaaLeague,
  ncaaPayload,
);

testCreateLeague(
  'leaguesApi.createFootballNflLeague',
  '/ui/leagues/football/nfl',
  leaguesApi.createFootballNflLeague,
  nflPayload,
);

testCreateLeague(
  'leaguesApi.createBaseballMlbLeague',
  '/ui/leagues/baseball/mlb',
  leaguesApi.createBaseballMlbLeague,
  mlbPayload,
);

describe('leaguesApi.getUserLeagues', () => {
  beforeEach(() => {
    (apiClient.get as jest.Mock).mockReset();
    (apiClient.get as jest.Mock).mockResolvedValue({ data: [] });
  });

  // Default omits the param entirely rather than sending includeDeactivated=false,
  // so the request matches the BE's default contract.
  it('GETs /ui/leagues without the param by default', async () => {
    await leaguesApi.getUserLeagues();

    expect(apiClient.get).toHaveBeenCalledTimes(1);
    expect(apiClient.get).toHaveBeenCalledWith('/ui/leagues', { params: undefined });
  });

  it('opts into deactivated leagues when asked', async () => {
    await leaguesApi.getUserLeagues({ includeDeactivated: true });

    expect(apiClient.get).toHaveBeenCalledWith('/ui/leagues', {
      params: { includeDeactivated: true },
    });
  });

  it('returns the server response body', async () => {
    const league: LeagueSummary = {
      id: 'league-1',
      name: 'MLB Test',
      sport: 'BaseballMlb',
      league: 'MLB',
      leagueType: 'StraightUp',
      useConfidencePoints: false,
      memberCount: 4,
      avatarUrl: null,
      deactivatedUtc: null,
    };
    (apiClient.get as jest.Mock).mockResolvedValue({ data: [league] });

    const response = await leaguesApi.getUserLeagues();
    expect(response.data).toEqual([league]);
  });
});

describe('leaguesApi.cloneLeague', () => {
  beforeEach(() => {
    (apiClient.post as jest.Mock).mockReset();
    (apiClient.post as jest.Mock).mockResolvedValue({ data: { id: 'clone-1' } });
  });

  it('POSTs to /ui/leagues/{id}/clone with the name and inviteMembers', async () => {
    await leaguesApi.cloneLeague('source-1', { name: 'MLB Test (Copy)', inviteMembers: true });

    expect(apiClient.post).toHaveBeenCalledTimes(1);
    expect(apiClient.post).toHaveBeenCalledWith('/ui/leagues/source-1/clone', {
      name: 'MLB Test (Copy)',
      inviteMembers: true,
    });
  });

  it('returns the new league id', async () => {
    const response = await leaguesApi.cloneLeague('source-1', {
      name: 'Copy',
      inviteMembers: false,
    });
    expect(response.data).toEqual({ id: 'clone-1' });
  });
});
