import { contrastTextOn, normalizeTeamColor, resolveTeamColors } from '@/src/utils/teamColor';

describe('normalizeTeamColor', () => {
  it('accepts hex with or without the hash, and 3-digit shorthand', () => {
    expect(normalizeTeamColor('#005030')).toBe('#005030');
    expect(normalizeTeamColor('005030')).toBe('#005030');
    expect(normalizeTeamColor('ABC')).toBe('#aabbcc');
  });

  it('returns null for anything that is not a hex color', () => {
    expect(normalizeTeamColor(null)).toBeNull();
    expect(normalizeTeamColor('')).toBeNull();
    expect(normalizeTeamColor('green')).toBeNull();
    expect(normalizeTeamColor('#12345')).toBeNull();
  });
});

describe('contrastTextOn', () => {
  it('uses white on dark team colors and dark on light ones', () => {
    expect(contrastTextOn('#005030')).toBe('#ffffff'); // Miami green
    expect(contrastTextOn('#000000')).toBe('#ffffff'); // Wake Forest black
    expect(contrastTextOn('#ffcc00')).toBe('#23272f'); // gold
    expect(contrastTextOn('#ffffff')).toBe('#23272f');
  });
});

describe('resolveTeamColors', () => {
  it('keeps two distinct real colors as they are', () => {
    expect(resolveTeamColors('#005030', '000000', '#1B3A6B', '#888')).toEqual({ away: '#005030', home: '#000000' });
  });

  it('sends the home side to the neutral when both teams lack a color', () => {
    // Otherwise the split bar and per-row bars are one indistinguishable navy.
    expect(resolveTeamColors(null, null, '#1B3A6B', '#888')).toEqual({ away: '#1b3a6b', home: '#888888' });
  });

  it('sends the home side to the neutral when both teams share a color', () => {
    expect(resolveTeamColors('#005030', '#005030', '#1B3A6B', '#6c757d')).toEqual({ away: '#005030', home: '#6c757d' });
  });

  it('only the missing side falls back', () => {
    expect(resolveTeamColors('#005030', 'not-a-color', '#1B3A6B', '#888')).toEqual({ away: '#005030', home: '#1b3a6b' });
  });

  it('detects a collision between a team color and the fallback written in a different case', () => {
    // "#1b3a6b" vs "#1B3A6B" paint the same navy; the guard compares normalized forms.
    expect(resolveTeamColors('#1B3A6B', null, '#1B3A6B', '#888')).toEqual({ away: '#1b3a6b', home: '#888888' });
    expect(resolveTeamColors(null, '1b3a6b', '#1B3A6B', '#888')).toEqual({ away: '#1b3a6b', home: '#888888' });
  });

  it('keeps substituting when the neutral itself collides', () => {
    // Two gray teams on the dark theme, whose neutral is that same gray.
    const r = resolveTeamColors('#6c757d', '#6c757d', '#1B3A6B', '#6c757d');
    expect(r.away).toBe('#6c757d');
    expect(r.home).not.toBe(r.away);
    expect(r.home).toBe('#1b3a6b');
  });
});
