import { describe, it, expect } from 'vitest';
import { formatBroadcasts } from './textUtils';

const NBSP = String.fromCharCode(0x00a0);

describe('formatBroadcasts', () => {
  it('replaces spaces inside network names with non-breaking spaces', () => {
    const result = formatBroadcasts(
      'Rangers Sports Network | MLB.TV | Chicago Sports Network'
    );

    expect(result).toBe(
      `Rangers${NBSP}Sports${NBSP}Network | MLB.TV | Chicago${NBSP}Sports${NBSP}Network`
    );
  });

  it('keeps ordinary breakable spaces around the separators', () => {
    const result = formatBroadcasts('ESPN | ABC');

    expect(result).toBe('ESPN | ABC');
    expect(result).not.toContain(`${NBSP}|`);
    expect(result).not.toContain(`|${NBSP}`);
  });

  it('passes a single network through with only internal spaces hardened', () => {
    expect(formatBroadcasts('Chicago Sports Network')).toBe(
      `Chicago${NBSP}Sports${NBSP}Network`
    );
    expect(formatBroadcasts('ESPN')).toBe('ESPN');
  });

  it('normalizes ragged separator spacing and drops empty segments', () => {
    expect(formatBroadcasts('ESPN|ABC')).toBe('ESPN | ABC');
    expect(formatBroadcasts('ESPN |  | ABC')).toBe('ESPN | ABC');
    expect(formatBroadcasts('')).toBe('');
  });
});
