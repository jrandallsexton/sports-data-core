import {
  leaguesApi,
  type CreateBaseballMlbLeagueRequest,
  type CreateFootballNcaaLeagueRequest,
  type CreateFootballNflLeagueRequest,
} from '@/src/services/api/leaguesApi';

// Mock the shared axios instance — we only care that the right URL + body go out.
jest.mock('@/src/services/api/client', () => ({
  apiClient: {
    post: jest.fn(),
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

// Each row pairs a sport helper with its expected endpoint + payload.
// The `method` is typed through `unknown` so the shared harness can invoke
// all three despite their distinct payload signatures.
const cases: Array<{
  name: string;
  endpoint: string;
  method: (payload: unknown) => Promise<{ data: unknown }>;
  payload: unknown;
}> = [
  {
    name: 'createFootballNcaaLeague',
    endpoint: '/ui/leagues/football/ncaa',
    method: leaguesApi.createFootballNcaaLeague as (p: unknown) => Promise<{ data: unknown }>,
    payload: ncaaPayload,
  },
  {
    name: 'createFootballNflLeague',
    endpoint: '/ui/leagues/football/nfl',
    method: leaguesApi.createFootballNflLeague as (p: unknown) => Promise<{ data: unknown }>,
    payload: nflPayload,
  },
  {
    name: 'createBaseballMlbLeague',
    endpoint: '/ui/leagues/baseball/mlb',
    method: leaguesApi.createBaseballMlbLeague as (p: unknown) => Promise<{ data: unknown }>,
    payload: mlbPayload,
  },
];

describe.each(cases)('leaguesApi.$name', ({ method, endpoint, payload }) => {
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
