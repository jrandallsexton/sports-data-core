import { useEffect } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useRememberedStandingsLeague } from '@/src/hooks/useRememberedStandingsLeague';

jest.mock('@react-native-async-storage/async-storage', () => ({
  getItem: jest.fn(),
  setItem: jest.fn(),
}));

const mockedStorage = AsyncStorage as unknown as {
  getItem: jest.Mock;
  setItem: jest.Mock;
};

describe('useRememberedStandingsLeague', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockedStorage.getItem.mockResolvedValue(null);
    mockedStorage.setItem.mockResolvedValue(undefined);
  });

  it('reports nothing remembered on a first launch', async () => {
    const { result } = renderHook(() => useRememberedStandingsLeague('user-1'));

    await waitFor(() => expect(result.current.hydrated).toBe(true));
    expect(result.current.rememberedLeagueId).toBeNull();
  });

  it('restores the league from the previous session, keyed by user', async () => {
    mockedStorage.getItem.mockResolvedValue('league-42');

    const { result } = renderHook(() => useRememberedStandingsLeague('user-1'));

    await waitFor(() => expect(result.current.hydrated).toBe(true));
    expect(result.current.rememberedLeagueId).toBe('league-42');
    expect(mockedStorage.getItem).toHaveBeenCalledWith('standings-last-league:user-1');
  });

  it('ignores remember() until the stored value has loaded', async () => {
    // The cold-start default (first league) lands before the read resolves;
    // writing it would overwrite the league the user actually left on.
    let resolveRead: (v: string | null) => void = () => {};
    mockedStorage.getItem.mockReturnValue(new Promise((r) => { resolveRead = r; }));

    const { result } = renderHook(() => useRememberedStandingsLeague('user-1'));
    expect(result.current.hydrated).toBe(false);

    act(() => result.current.remember('first-league'));
    expect(mockedStorage.setItem).not.toHaveBeenCalled();

    await act(async () => resolveRead('league-42'));
    expect(result.current.rememberedLeagueId).toBe('league-42');
  });

  it('persists a new league once hydrated, skipping repeats', async () => {
    mockedStorage.getItem.mockResolvedValue('league-42');
    const { result } = renderHook(() => useRememberedStandingsLeague('user-1'));
    await waitFor(() => expect(result.current.hydrated).toBe(true));

    act(() => result.current.remember('league-42')); // already stored
    expect(mockedStorage.setItem).not.toHaveBeenCalled();

    act(() => result.current.remember('league-7'));
    act(() => result.current.remember('league-7'));
    expect(mockedStorage.setItem).toHaveBeenCalledTimes(1);
    expect(mockedStorage.setItem).toHaveBeenCalledWith('standings-last-league:user-1', 'league-7');
  });

  it("does not expose or write the previous user's league on an account switch", async () => {
    // uid A -> B without unmounting (in-place account switch). Mirrors the
    // Standings screen: remember() is called from an effect in the SAME commit
    // as the uid change, and every render's values are recorded. renderHook's
    // rerender flushes effects inside act(), so checking result.current
    // afterwards would miss the switch render entirely.
    const renders: Array<{ uid: string; hydrated: boolean; remembered: string | null }> = [];
    mockedStorage.getItem.mockResolvedValueOnce('league-a');
    const { result, rerender } = renderHook(
      ({ uid, selected }: { uid: string; selected: string }) => {
        const r = useRememberedStandingsLeague(uid);
        renders.push({ uid, hydrated: r.hydrated, remembered: r.rememberedLeagueId });
        const { remember } = r;
        useEffect(() => {
          remember(selected);
        }, [selected, remember]);
        return r;
      },
      { initialProps: { uid: 'user-a', selected: 'league-a' } },
    );
    await waitFor(() => expect(result.current.rememberedLeagueId).toBe('league-a'));
    expect(mockedStorage.setItem).not.toHaveBeenCalled(); // already stored for A

    let resolveB: (v: string | null) => void = () => {};
    mockedStorage.getItem.mockReturnValueOnce(new Promise((r) => { resolveB = r; }));
    rerender({ uid: 'user-b', selected: 'league-a' });

    // Every render for B before its read resolves: nothing of A's visible.
    const userBRenders = renders.filter((r) => r.uid === 'user-b');
    expect(userBRenders.length).toBeGreaterThan(0);
    for (const r of userBRenders) {
      expect(r.hydrated).toBe(false);
      expect(r.remembered).toBeNull();
    }
    // ...and A's selection was not written under B's key.
    expect(mockedStorage.setItem).not.toHaveBeenCalled();

    await act(async () => resolveB('league-b'));
    expect(result.current.hydrated).toBe(true);
    expect(result.current.rememberedLeagueId).toBe('league-b');
  });

  it('treats a failed read as nothing remembered', async () => {
    mockedStorage.getItem.mockRejectedValue(new Error('storage unavailable'));

    const { result } = renderHook(() => useRememberedStandingsLeague('user-1'));

    await waitFor(() => expect(result.current.hydrated).toBe(true));
    expect(result.current.rememberedLeagueId).toBeNull();
  });

  it('neither reads nor writes without a signed-in user', async () => {
    const { result } = renderHook(() => useRememberedStandingsLeague(undefined));

    await waitFor(() => expect(result.current.hydrated).toBe(true));
    act(() => result.current.remember('league-7'));
    expect(mockedStorage.getItem).not.toHaveBeenCalled();
    expect(mockedStorage.setItem).not.toHaveBeenCalled();
  });
});
