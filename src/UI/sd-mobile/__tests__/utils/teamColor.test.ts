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
    expect(resolveTeamColors(null, null, '#1B3A6B', '#888')).toEqual({ away: '#1B3A6B', home: '#888888' });
  });

  it('sends the home side to the neutral when both teams share a color', () => {
    expect(resolveTeamColors('#005030', '#005030', '#1B3A6B', '#6c757d')).toEqual({ away: '#005030', home: '#6c757d' });
  });

  it('only the missing side falls back', () => {
    expect(resolveTeamColors('#005030', 'not-a-color', '#1B3A6B', '#888')).toEqual({ away: '#005030', home: '#1B3A6B' });
  });
});
