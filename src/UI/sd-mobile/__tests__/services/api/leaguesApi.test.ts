import { leaguesApi, type CreateFootballNcaaLeagueRequest } from '@/src/services/api/leaguesApi';

// Mock the shared axios instance — we only care that the right URL + body go out.
jest.mock('@/src/services/api/client', () => ({
  apiClient: {
    post: jest.fn(),
  },
}));

// eslint-disable-next-line @typescript-eslint/no-require-imports
const { apiClient } = require('@/src/services/api/client');

describe('leaguesApi.createFootballNcaaLeague', () => {
  beforeEach(() => {
    (apiClient.post as jest.Mock).mockReset();
    (apiClient.post as jest.Mock).mockResolvedValue({ data: { id: 'league-1' } });
  });

  const payload: CreateFootballNcaaLeagueRequest = {
    name: 'Saturday Showdown',
    description: 'SEC only',
    pickType: 'StraightUp',
    tiebreakerType: 'TotalPoints',
    tiebreakerTiePolicy: 'EarliestSubmission',
    useConfidencePoints: false,
    isPublic: false,
    dropLowWeeksCount: 0,
    startsOn: null,
    endsOn: null,
    rankingFilter: null,
    conferenceSlugs: [],
  };

  it('POSTs to /ui/leagues/football/ncaa with the given payload', async () => {
    await leaguesApi.createFootballNcaaLeague(payload);

    expect(apiClient.post).toHaveBeenCalledTimes(1);
    expect(apiClient.post).toHaveBeenCalledWith('/ui/leagues/football/ncaa', payload);
  });

  it('returns the server response body', async () => {
    const response = await leaguesApi.createFootballNcaaLeague(payload);
    expect(response.data).toEqual({ id: 'league-1' });
  });
});
