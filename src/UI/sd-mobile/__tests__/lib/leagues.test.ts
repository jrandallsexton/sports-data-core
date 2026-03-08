import { getLeagues } from '@/src/lib/leagues';

describe('getLeagues', () => {
  it('returns leagues array when present', () => {
    const leagues = [{ id: '1', name: 'Test League' }];
    const result = getLeagues({ leagues } as any);
    expect(result).toEqual(leagues);
  });

  it('returns empty array when leagues is undefined', () => {
    expect(getLeagues({ leagues: undefined })).toEqual([]);
  });

  it('returns empty array when input is undefined', () => {
    expect(getLeagues(undefined)).toEqual([]);
  });
});
