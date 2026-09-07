import { create } from 'zustand';

/**
 * The app-wide "current league" — the league the user most recently chose by
 * an EXPLICIT action (a league chip on Picks/Standings, a Home league card).
 *
 * Contract (see the By Week PR discussion):
 *  - Only explicit user actions write. Surfaces that auto-select a fallback
 *    (reconciliation snaps, first-league defaults) must NOT write it back —
 *    two tabs with different league lists would ping-pong the store.
 *  - Consumers read-and-validate: adopt the id only when it exists in their
 *    own league list; otherwise keep their local default and leave the store
 *    untouched.
 *  - Session-scoped (in-memory): an app restart re-defaults every surface.
 */
interface LeagueSelectionState {
  selectedLeagueId: string | null;
  setSelectedLeague: (id: string) => void;
}

export const useLeagueSelectionStore = create<LeagueSelectionState>((set) => ({
  selectedLeagueId: null,
  setSelectedLeague: (id) => set({ selectedLeagueId: id }),
}));
