import { create } from 'zustand';

/**
 * The app-wide "current league" — the league the user most recently chose by
 * an EXPLICIT action (a league chip on Picks/Standings, a Home league card,
 * a deep link).
 *
 * Contract (see the By Week PR discussion):
 *  - Only explicit user actions write. Surfaces that auto-select a fallback
 *    (reconciliation snaps, first-league defaults) must NOT write it back —
 *    two tabs with different league lists would ping-pong the store.
 *  - Consumers read-and-validate: adopt the id only when it exists in their
 *    own league list; otherwise keep their local default and leave the store
 *    untouched.
 *  - Every explicit choice bumps selectionNonce, INCLUDING re-choosing the
 *    id already stored. Zustand skips notifying on same-value sets, so
 *    without the nonce a user who locally browsed away on one surface and
 *    then re-tapped the same league on another would produce no signal at
 *    all. Consumers key adoption on the nonce and consume each one once.
 *  - Session-scoped (in-memory): an app restart re-defaults every surface.
 */
interface LeagueSelectionState {
  selectedLeagueId: string | null;
  /** Monotonic; bumps on every explicit choice, same-id re-choices included. */
  selectionNonce: number;
  setSelectedLeague: (id: string) => void;
}

export const useLeagueSelectionStore = create<LeagueSelectionState>((set) => ({
  selectedLeagueId: null,
  selectionNonce: 0,
  setSelectedLeague: (id) =>
    set((s) => ({ selectedLeagueId: id, selectionNonce: s.selectionNonce + 1 })),
}));
