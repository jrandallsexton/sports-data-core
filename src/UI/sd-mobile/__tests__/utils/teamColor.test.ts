import { contrastTextOn, normalizeTeamColor } from '@/src/utils/teamColor';

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
