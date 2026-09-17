import { colorDistance, colorsDistinguishable, contrastTextOn, normalizeTeamColor, resolveTeamColors } from '@/src/utils/teamColor';

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

describe('colorDistance', () => {
  it('is zero for the same color and grows with difference', () => {
    expect(colorDistance('#ba0c2f', '#ba0c2f')).toBe(0);
    expect(colorDistance('#ba0c2f', '#a41f35')).toBeLessThan(colorDistance('#005030', '#000000'));
  });
});

describe('colorsDistinguishable', () => {
  it.each([
    ['#ba0c2f', '#a41f35', 'Georgia vs Arkansas red'],
    ['#9e2237', '#d21034', 'USC vs Rutgers red'],
    ['#bb0000', '#990000', 'crimson vs dark red'],
    ['#ff8200', '#f56600', 'Tennessee vs Clemson orange'],
    ['#00274c', '#041e42', 'Michigan vs Penn State navy'],
    ['#154733', '#18453b', 'Oregon vs Michigan State green'],
    ['#6c757d', '#9ca3af', 'two grays'],
  ])('treats %s and %s as the same family (%s)', (a, b) => {
    expect(colorsDistinguishable(a, b)).toBe(false);
  });

  it.each([
    ['#005030', '#000000', 'Miami green vs Wake Forest black'],
    ['#bb0000', '#00274c', 'Ohio State red vs Michigan navy'],
    ['#003087', '#7bafd4', 'Duke blue vs UNC powder blue - same hue, big lightness gap'],
    ['#1b3a6b', '#000000', 'navy vs black'],
    ['#000000', '#ffffff', 'black vs white'],
    ['#f1b82d', '#000000', 'Missouri gold vs black'],
  ])('tells %s and %s apart (%s)', (a, b) => {
    expect(colorsDistinguishable(a, b)).toBe(true);
  });
});

describe('resolveTeamColors on near-identical team colors', () => {
  it('sends the home side to the neutral when two reds cannot be told apart (Georgia vs Arkansas)', () => {
    const r = resolveTeamColors('#ba0c2f', '#a41f35', '#1B3A6B', '#6c757d');
    expect(r.away).toBe('#ba0c2f');
    expect(r.home).toBe('#6c757d');
  });

  it('does the same for two reds that are far apart in RGB but the same to the eye (USC vs Rutgers)', () => {
    const r = resolveTeamColors('#9e2237', '#d21034', '#1B3A6B', '#6c757d');
    expect(r.home).toBe('#6c757d');
  });

  it('keeps two clearly different colors (Miami green vs Wake Forest black)', () => {
    expect(resolveTeamColors('#005030', '#000000', '#1B3A6B', '#6c757d')).toEqual({ away: '#005030', home: '#000000' });
  });

  it('skips a neutral that is itself too close to the away color', () => {
    // A gray team on the dark theme whose neutral is a similar gray.
    const r = resolveTeamColors('#6c757d', '#6c757d', '#1B3A6B', '#7a8390');
    expect(r.home).toBe('#1b3a6b');
  });
});
