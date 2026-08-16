import { describe, it, expect, vi } from 'vitest';

import { anySportOpenForCreation } from './leagueCreationGates';

// Mock the API module so importing the util doesn't pull in the real axios
// client chain — these tests only exercise the pure helper. (vi.mock is
// hoisted above the imports at runtime, so placement here is safe.)
vi.mock('../api/leagues/leaguesApi', () => ({
  default: { getCreationAvailability: vi.fn() },
}));

describe('anySportOpenForCreation', () => {
  const FUTURE = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();
  const PAST = new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString();

  it('is closed when every non-admin sport has a future gate', () => {
    expect(
      anySportOpenForCreation({ FootballNcaa: FUTURE, FootballNfl: FUTURE })
    ).toBe(false);
  });

  it('is open when a sport is absent from the gates (API omits elapsed gates)', () => {
    expect(anySportOpenForCreation({ FootballNfl: FUTURE })).toBe(true);
  });

  it('is open when a gate instant has elapsed (defensive against contract drift)', () => {
    expect(
      anySportOpenForCreation({ FootballNcaa: PAST, FootballNfl: FUTURE })
    ).toBe(true);
  });

  it('is open on an empty or missing map (no gates / resolved fetch failure)', () => {
    expect(anySportOpenForCreation({})).toBe(true);
    expect(anySportOpenForCreation(undefined)).toBe(true);
  });

  it('ignores gates for sports non-admins cannot create (MLB)', () => {
    expect(
      anySportOpenForCreation({
        FootballNcaa: FUTURE,
        FootballNfl: FUTURE,
        BaseballMlb: PAST,
      })
    ).toBe(false);
  });
});
