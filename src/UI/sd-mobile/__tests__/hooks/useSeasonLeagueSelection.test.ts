import { renderHook, act } from '@testing-library/react-native';
import { useSeasonLeagueSelection } from '@/src/hooks/useSeasonLeagueSelection';
import type { LeagueSummary } from '@/src/services/api/leaguesApi';

function league(
  partial: Partial<LeagueSummary> & { id: string; seasonYear: number },
): LeagueSummary {
  return {
    name: partial.id,
    sport: 'BaseballMlb',
    league: 'MLB',
    leagueType: 'StraightUp',
    useConfidencePoints: false,
    memberCount: 1,
    avatarUrl: null,
    seasonWeeks: [],
    deactivatedUtc: null,
    createdUtc: '2025-01-01T00:00:00Z',
    ...partial,
  };
}

describe('useSeasonLeagueSelection', () => {
  it('returns empty derivations when the user has no leagues', () => {
    const { result } = renderHook(() => useSeasonLeagueSelection([]));

    expect(result.current.seasons).toEqual([]);
    expect(result.current.selectedSeason).toBeNull();
    expect(result.current.seasonLeagues).toEqual([]);
    expect(result.current.selectedLeagueId).toBeNull();
  });

  it('defaults to the newest season, active-only, and selects the first league', () => {
    const leagues = [
      league({ id: 'active', seasonYear: 2026 }),
      league({ id: 'ended', seasonYear: 2026, deactivatedUtc: '2026-01-01T00:00:00Z' }),
    ];

    const { result } = renderHook(() => useSeasonLeagueSelection(leagues));

    expect(result.current.seasons).toEqual([2026]);
    expect(result.current.selectedSeason).toBe(2026);
    expect(result.current.canFilterEnded).toBe(true);
    // Active-only by default: the deactivated league is excluded.
    expect(result.current.seasonLeagues.map((l) => l.id)).toEqual(['active']);
    expect(result.current.selectedLeagueId).toBe('active');
  });

  it('reveals ended leagues when showEnded is toggled on', () => {
    const leagues = [
      league({ id: 'active', seasonYear: 2026 }),
      league({ id: 'ended', seasonYear: 2026, deactivatedUtc: '2026-01-01T00:00:00Z' }),
    ];

    const { result } = renderHook(() => useSeasonLeagueSelection(leagues));

    act(() => result.current.setShowEnded(true));

    expect(result.current.seasonLeagues.map((l) => l.id).sort()).toEqual(['active', 'ended']);
  });

  it('sorts leagues by name so ordering and the snap target are deterministic', () => {
    const leagues = [
      league({ id: 'z', name: 'Zeta', seasonYear: 2026 }),
      league({ id: 'a', name: 'Alpha', seasonYear: 2026 }),
      league({ id: 'm', name: 'Mu', seasonYear: 2026 }),
    ];

    const { result } = renderHook(() => useSeasonLeagueSelection(leagues));

    expect(result.current.seasonLeagues.map((l) => l.name)).toEqual(['Alpha', 'Mu', 'Zeta']);
    // Snap target is the first in sorted order, not the input order.
    expect(result.current.selectedLeagueId).toBe('a');
  });

  it('treats a prior season as all-ended: no ended filter, every league shown', () => {
    const leagues = [
      league({ id: 'current', seasonYear: 2026 }),
      league({ id: 'past', seasonYear: 2025, deactivatedUtc: '2025-12-01T00:00:00Z' }),
    ];

    const { result } = renderHook(() => useSeasonLeagueSelection(leagues));

    // Newest season is the default.
    expect(result.current.seasons).toEqual([2026, 2025]);
    expect(result.current.selectedSeason).toBe(2026);

    // Switching to the past season disables the ended filter and shows all its
    // leagues; the selected league snaps into the new season.
    act(() => result.current.setSelectedSeason(2025));

    expect(result.current.canFilterEnded).toBe(false);
    expect(result.current.seasonLeagues.map((l) => l.id)).toEqual(['past']);
    expect(result.current.selectedLeagueId).toBe('past');
  });

  describe('preferredLeagueId adoption (app-wide league store)', () => {
    const leagues = [
      league({ id: 'a-2026', seasonYear: 2026 }),
      league({ id: 'b-2026', seasonYear: 2026 }),
      league({ id: 'old-2025', seasonYear: 2025 }),
      league({ id: 'ended-2026', seasonYear: 2026, deactivatedUtc: '2026-01-01T00:00:00Z' }),
    ];

    it('adopts the preferred league and flips the season along', () => {
      const { result } = renderHook(() => useSeasonLeagueSelection(leagues, 'old-2025', 1));

      expect(result.current.selectedLeagueId).toBe('old-2025');
      expect(result.current.selectedSeason).toBe(2025);
    });

    it('adopts an ended current-season league by revealing ended leagues', () => {
      const { result } = renderHook(() => useSeasonLeagueSelection(leagues, 'ended-2026', 1));

      expect(result.current.selectedLeagueId).toBe('ended-2026');
      expect(result.current.showEnded).toBe(true);
    });

    it('consumes each nonce once — local browsing is never yanked back', () => {
      const { result } = renderHook(() => useSeasonLeagueSelection(leagues, 'old-2025', 1));
      expect(result.current.selectedLeagueId).toBe('old-2025');

      // The user browses back to 2026; reconciliation snaps to the first
      // visible league. The still-set preferred id must NOT re-adopt.
      act(() => result.current.setSelectedSeason(2026));
      expect(result.current.selectedSeason).toBe(2026);
      expect(result.current.selectedLeagueId).not.toBe('old-2025');
    });

    it('a preference equal to the current selection still consumes its nonce', () => {
      // CR-738: pref arrives naming the league we already defaulted to; if
      // the nonce is not recorded on that path, a later season browse makes
      // pref differ from the selection and the STALE choice re-adopts.
      const { result, rerender } = renderHook(
        ({ pref, nonce }: { pref: string | null; nonce: number }) =>
          useSeasonLeagueSelection(leagues, pref, nonce),
        { initialProps: { pref: null as string | null, nonce: 0 } },
      );
      expect(result.current.selectedLeagueId).toBe('a-2026'); // default

      rerender({ pref: 'a-2026', nonce: 1 }); // matches current selection

      act(() => result.current.setSelectedSeason(2025));
      expect(result.current.selectedLeagueId).toBe('old-2025'); // snap
      // The consumed nonce must not drag the selection back to a-2026.
      expect(result.current.selectedSeason).toBe(2025);
    });

    it('re-choosing the SAME league elsewhere (new nonce) converges again', () => {
      // Vortex-738: after local browsing diverges, a fresh explicit tap of
      // the same league on another surface must still propagate.
      const { result, rerender } = renderHook(
        ({ nonce }: { nonce: number }) => useSeasonLeagueSelection(leagues, 'old-2025', nonce),
        { initialProps: { nonce: 1 } },
      );
      expect(result.current.selectedLeagueId).toBe('old-2025');

      act(() => result.current.setSelectedSeason(2026)); // browse away, snap
      expect(result.current.selectedLeagueId).not.toBe('old-2025');

      rerender({ nonce: 2 }); // same league, fresh explicit choice
      expect(result.current.selectedLeagueId).toBe('old-2025');
      expect(result.current.selectedSeason).toBe(2025);
    });

    it('ignores a preferred id that is not one of the user leagues', () => {
      const { result } = renderHook(() => useSeasonLeagueSelection(leagues, 'foreign', 1));

      expect(result.current.selectedLeagueId).toBe('a-2026');
    });
  });
});
