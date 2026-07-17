import { seasonApi, type CurrentSeason } from '@/src/services/api/seasonApi';

// Mock the shared axios instance — we only care about the URL that goes out.
jest.mock('@/src/services/api/client', () => ({
  apiClient: {
    get: jest.fn(),
  },
}));

// eslint-disable-next-line @typescript-eslint/no-require-imports
const { apiClient } = require('@/src/services/api/client');

describe('seasonApi.getCurrentSeason', () => {
  beforeEach(() => {
    (apiClient.get as jest.Mock).mockReset();
    (apiClient.get as jest.Mock).mockResolvedValue({ data: null });
  });

  it('GETs the per-sport current-season route', async () => {
    await seasonApi.getCurrentSeason('football', 'ncaa');

    expect(apiClient.get).toHaveBeenCalledTimes(1);
    expect(apiClient.get).toHaveBeenCalledWith('/api/football/ncaa/seasons/current');
  });

  it('returns the season with its phases', async () => {
    const season: CurrentSeason = {
      seasonYear: 2026,
      name: '2026 Season',
      startDate: '2026-08-29T00:00:00Z',
      endDate: '2027-01-15T00:00:00Z',
      phases: [
        { typeCode: 2, name: 'Regular Season', startDate: '2026-08-29T00:00:00Z', endDate: '2026-12-07T00:00:00Z' },
      ],
    };
    (apiClient.get as jest.Mock).mockResolvedValue({ data: season });

    const res = await seasonApi.getCurrentSeason('football', 'nfl');
    expect(res.data).toEqual(season);
  });
});
