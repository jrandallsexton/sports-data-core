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
});
