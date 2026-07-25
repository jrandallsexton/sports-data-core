// Mock the shared axios instance so importing the hook module doesn't pull in
// the real API client (and its firebase chain) — we only exercise the module's
// pure date helpers here. Mirrors __tests__/services/api/leaguesApi.test.ts.
jest.mock('@/src/services/api/client', () => ({
  apiClient: {
    get: jest.fn(),
    post: jest.fn(),
  },
}));

import {
  formatGateDate,
  formatGateDateOrSoon,
} from '@/src/hooks/useLeagueCreationGates';

describe('formatGateDate', () => {
  it('formats a valid ISO instant in UTC (no timezone shift)', () => {
    // Midnight UTC on Aug 17 must read "Aug 17", not roll back a day in
    // western timezones.
    expect(formatGateDate('2026-08-17T00:00:00Z')).toBe('Aug 17');
  });

  it('returns null for an unparseable value', () => {
    expect(formatGateDate('not-a-date')).toBeNull();
  });
});

describe('formatGateDateOrSoon', () => {
  it('returns the formatted date for a valid instant', () => {
    expect(formatGateDateOrSoon('2026-09-01T00:00:00Z')).toBe('Sep 1');
  });

  it('falls back to "soon" for an unparseable value (never renders "null")', () => {
    expect(formatGateDateOrSoon('nope')).toBe('soon');
  });
});
