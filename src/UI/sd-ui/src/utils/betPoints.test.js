import { describe, it, expect } from 'vitest';
import { formatBetPoints, betPointsTone } from './betPoints';

describe('formatBetPoints', () => {
  it('signs a winning week and rounds to two places', () => {
    expect(formatBetPoints(2.2622)).toBe('+2.26');
  });

  it('keeps the minus on a losing week', () => {
    expect(formatBetPoints(-4.2143)).toBe('-4.21');
  });

  it('shows an even week, including one that rounds to -0, as 0.00', () => {
    expect(formatBetPoints(0)).toBe('0.00');
    expect(formatBetPoints(-0.004)).toBe('0.00');
  });

  it('never prints a currency sign', () => {
    expect(formatBetPoints(1.5)).not.toContain('$');
  });
});

describe('betPointsTone', () => {
  it('judges the tone on the displayed value', () => {
    expect(betPointsTone(0.9091)).toBe('positive');
    expect(betPointsTone(-1)).toBe('negative');
    expect(betPointsTone(0.004)).toBe('even');
  });
});
