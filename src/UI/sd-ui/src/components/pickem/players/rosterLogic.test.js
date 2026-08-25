import { describe, it, expect } from 'vitest';
import {
  SLOT_DEFS,
  eligiblePositions,
  canAssign,
  assign,
  remove,
  isRostered,
  firstOpenSlotFor,
} from './rosterLogic';

const qb = { athleteId: 'qb-1', position: 'QB', firstName: 'Arch', lastName: 'Manning' };
const rb = { athleteId: 'rb-1', position: 'RB', firstName: 'Jeremiyah', lastName: 'Love' };
const wr = { athleteId: 'wr-1', position: 'WR', firstName: 'Jeremiah', lastName: 'Smith' };
const te = { athleteId: 'te-1', position: 'TE', firstName: 'Max', lastName: 'Klare' };

describe('SLOT_DEFS', () => {
  it('is the fixed v1 shape with DEF present but disabled', () => {
    expect(SLOT_DEFS.map((s) => s.id)).toEqual([
      'QB', 'RB1', 'RB2', 'WR1', 'WR2', 'TE', 'FLEX', 'K', 'DEF',
    ]);
    expect(SLOT_DEFS.find((s) => s.id === 'DEF').disabled).toBe(true);
  });
});

describe('eligiblePositions', () => {
  it('FLEX accepts RB, WR, and TE', () => {
    expect(eligiblePositions('FLEX')).toEqual(['RB', 'WR', 'TE']);
  });

  it('unknown slot yields no positions', () => {
    expect(eligiblePositions('NOPE')).toEqual([]);
  });
});

describe('canAssign / assign', () => {
  it('fills an eligible empty slot', () => {
    const roster = assign({}, 'QB', qb);
    expect(roster.QB).toBe(qb);
  });

  it('rejects a position mismatch and returns the SAME roster object', () => {
    const before = {};
    expect(canAssign(before, 'QB', rb)).toBe(false);
    expect(assign(before, 'QB', rb)).toBe(before);
  });

  it('rejects the disabled DEF slot', () => {
    expect(canAssign({}, 'DEF', rb)).toBe(false);
  });

  it('rejects an athlete already rostered in another slot', () => {
    const roster = assign({}, 'RB1', rb);
    expect(canAssign(roster, 'RB2', rb)).toBe(false);
    expect(canAssign(roster, 'FLEX', rb)).toBe(false);
  });

  it('allows re-assigning into the same slot (no-op replace)', () => {
    const roster = assign({}, 'RB1', rb);
    expect(canAssign(roster, 'RB1', rb)).toBe(true);
  });

  it('replaces an occupied slot with a different athlete', () => {
    const other = { ...wr, athleteId: 'wr-2', lastName: 'Williams' };
    const roster = assign(assign({}, 'WR1', wr), 'WR1', other);
    expect(roster.WR1).toBe(other);
    expect(isRostered(roster, wr.athleteId)).toBe(false);
  });
});

describe('remove', () => {
  it('empties the slot and returns the same object when already empty', () => {
    const roster = assign({}, 'TE', te);
    const next = remove(roster, 'TE');
    expect(next.TE).toBeUndefined();
    expect(remove(next, 'TE')).toBe(next);
  });
});

describe('firstOpenSlotFor', () => {
  it('prefers the position slot before FLEX', () => {
    expect(firstOpenSlotFor({}, rb)).toBe('RB1');
    const oneRb = assign({}, 'RB1', rb);
    const otherRb = { ...rb, athleteId: 'rb-2' };
    expect(firstOpenSlotFor(oneRb, otherRb)).toBe('RB2');
  });

  it('falls through to FLEX when position slots are full', () => {
    const other = { ...te, athleteId: 'te-2' };
    const roster = assign({}, 'TE', te);
    expect(firstOpenSlotFor(roster, other)).toBe('FLEX');
  });

  it('returns null when nothing is open', () => {
    let roster = assign({}, 'TE', te);
    roster = assign(roster, 'FLEX', { ...te, athleteId: 'te-2' });
    expect(firstOpenSlotFor(roster, { ...te, athleteId: 'te-3' })).toBeNull();
  });
});
