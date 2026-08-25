// Pure lineup-slot rules for the Player Pick'em roster builder — TS port
// of sd-ui's src/components/pickem/players/rosterLogic.js. Keep the two
// in lockstep; the rules move server-side unchanged when PlayerLineup
// entities arrive.

import type { PickemAthlete } from '@/src/services/api/playerPickemApi';

export type SlotDef = {
  id: string;
  label: string;
  eligible: string[];
  disabled?: boolean;
};

export type Roster = Record<string, PickemAthlete | undefined>;

// v1 fixed shape per docs/features/player-pickem/player-pickem.md open
// question #1. DEF is a team pick, not an athlete pick — present but
// disabled until the team-defense picker exists.
export const SLOT_DEFS: SlotDef[] = [
  { id: 'QB', label: 'QB', eligible: ['QB'] },
  { id: 'RB1', label: 'RB', eligible: ['RB'] },
  { id: 'RB2', label: 'RB', eligible: ['RB'] },
  { id: 'WR1', label: 'WR', eligible: ['WR'] },
  { id: 'WR2', label: 'WR', eligible: ['WR'] },
  { id: 'TE', label: 'TE', eligible: ['TE'] },
  { id: 'FLEX', label: 'FLEX', eligible: ['RB', 'WR', 'TE'] },
  { id: 'K', label: 'K', eligible: ['K'] },
  { id: 'DEF', label: 'DEF', eligible: [], disabled: true },
];

export function slotById(slotId: string): SlotDef | null {
  return SLOT_DEFS.find((s) => s.id === slotId) ?? null;
}

export function eligiblePositions(slotId: string): string[] {
  return slotById(slotId)?.eligible ?? [];
}

/**
 * An athlete may fill a slot iff the slot exists and is enabled, the
 * athlete's position is eligible for it, and the athlete isn't already
 * rostered in ANOTHER slot (re-assigning into the same slot is fine —
 * that's a no-op replace).
 */
export function canAssign(
  roster: Roster,
  slotId: string,
  athlete: PickemAthlete
): boolean {
  const slot = slotById(slotId);
  if (!slot || slot.disabled) return false;
  if (!slot.eligible.includes(athlete.position)) return false;

  return !Object.entries(roster).some(
    ([id, rostered]) =>
      id !== slotId && rostered?.athleteId === athlete.athleteId
  );
}

/** Returns a new roster with the athlete in the slot (or the same roster
 *  object if the assignment is illegal — callers can reference-compare). */
export function assign(
  roster: Roster,
  slotId: string,
  athlete: PickemAthlete
): Roster {
  if (!canAssign(roster, slotId, athlete)) return roster;
  return { ...roster, [slotId]: athlete };
}

export function remove(roster: Roster, slotId: string): Roster {
  if (!(slotId in roster)) return roster;
  const next = { ...roster };
  delete next[slotId];
  return next;
}

export function isRostered(roster: Roster, athleteId: string): boolean {
  return Object.values(roster).some((a) => a?.athleteId === athleteId);
}
