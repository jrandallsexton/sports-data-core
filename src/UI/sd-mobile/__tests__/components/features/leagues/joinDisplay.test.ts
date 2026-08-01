import {
  joinClosesState,
  formatRemaining,
  windowLabel,
} from '@/src/components/features/leagues/joinDisplay';

// The join-status logic is the shared contract with sd-ui's JoinClosesLabel;
// these lock the four states so the two apps can't drift.
describe('joinClosesState', () => {
  const NOW = Date.parse('2026-09-01T12:00:00Z');
  const iso = (ms: number) => new Date(NOW + ms).toISOString();
  const HOUR = 60 * 60 * 1000;
  const DAY = 24 * HOUR;

  it('is Open when there is no close instant', () => {
    expect(joinClosesState(null, true, NOW)).toEqual({ text: 'Open', kind: 'open' });
  });

  it('is Closed when isJoinable is false, regardless of the date', () => {
    expect(joinClosesState(iso(5 * DAY), false, NOW).kind).toBe('closed');
  });

  it('is Closed once the close instant has passed', () => {
    expect(joinClosesState(iso(-HOUR), true, NOW).kind).toBe('closed');
  });

  it('shows a live countdown inside the 10-day window', () => {
    const s = joinClosesState(iso(2 * DAY + 4 * HOUR), true, NOW);
    expect(s.kind).toBe('countdown');
    expect(s.text).toBe('Closes in 2d 4h');
  });

  it('shows a plain date beyond 10 days', () => {
    const s = joinClosesState(iso(20 * DAY), true, NOW);
    expect(s.kind).toBe('date');
    expect(s.text.startsWith('Closes ')).toBe(true);
    expect(s.text).not.toContain('in ');
  });

  it('treats exactly the 10-day boundary as a countdown', () => {
    expect(joinClosesState(iso(10 * DAY), true, NOW).kind).toBe('countdown');
  });

  it('falls back to Open on an unparseable close instant', () => {
    expect(joinClosesState('not-a-date', true, NOW)).toEqual({ text: 'Open', kind: 'open' });
  });
});

describe('formatRemaining', () => {
  const M = 60 * 1000;
  const H = 60 * M;
  const D = 24 * H;
  it('renders days+hours, hours+minutes, or minutes', () => {
    expect(formatRemaining(2 * D + 4 * H)).toBe('2d 4h');
    expect(formatRemaining(3 * H + 15 * M)).toBe('3h 15m');
    expect(formatRemaining(42 * M)).toBe('42m');
    expect(formatRemaining(-1000)).toBe('0m');
  });
});

describe('windowLabel', () => {
  it('covers full-season, range, and one-sided windows', () => {
    expect(windowLabel(null, null)).toBe('Full Season');
    expect(windowLabel('2026-09-01T00:00:00Z', '2026-09-30T00:00:00Z')).toContain('–');
    expect(windowLabel('2026-09-01T00:00:00Z', null)).toMatch(/^From /);
    expect(windowLabel(null, '2026-09-30T00:00:00Z')).toMatch(/^Through /);
  });
});
