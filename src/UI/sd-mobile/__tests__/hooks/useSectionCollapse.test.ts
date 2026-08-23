import { act, renderHook, waitFor } from '@testing-library/react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useSectionCollapse } from '@/src/hooks/useSectionCollapse';
import { useSectionCollapseStore } from '@/src/stores/sectionCollapseStore';

jest.mock('@react-native-async-storage/async-storage', () => ({
  getItem: jest.fn(),
  setItem: jest.fn(),
}));

const mockedStorage = AsyncStorage as unknown as {
  getItem: jest.Mock;
  setItem: jest.Mock;
};

describe('useSectionCollapse', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockedStorage.getItem.mockResolvedValue(null);
    mockedStorage.setItem.mockResolvedValue(undefined);
    // The store is a module singleton; clear it so each test starts cold.
    useSectionCollapseStore.setState({ collapsed: {}, hydrated: false });
  });

  it('starts expanded when nothing has been stored', async () => {
    // The card surfaces context without being asked, so a section the user
    // has never touched must not be hidden.
    const { result } = renderHook(() => useSectionCollapse('history.headToHead'));

    await waitFor(() => expect(mockedStorage.getItem).toHaveBeenCalled());
    expect(result.current.collapsed).toBe(false);
  });

  it('restores a previous collapse so the choice survives an app restart', async () => {
    mockedStorage.getItem.mockResolvedValue('true');

    const { result } = renderHook(() => useSectionCollapse('history.lastSeason'));

    await waitFor(() => expect(result.current.collapsed).toBe(true));
  });

  it('shares state across mounted instances', async () => {
    // The reported bug. Every MatchupCard renders its comparison modal
    // eagerly, so with per-instance state each modal read storage once at
    // list-render time and never saw a later toggle: collapsing on one game
    // then opening another showed the section expanded again.
    const gameA = renderHook(() => useSectionCollapse('history.headToHead'));
    const gameB = renderHook(() => useSectionCollapse('history.headToHead'));

    await waitFor(() => expect(mockedStorage.getItem).toHaveBeenCalled());
    expect(gameB.result.current.collapsed).toBe(false);

    act(() => gameA.result.current.toggle());

    expect(gameA.result.current.collapsed).toBe(true);
    expect(gameB.result.current.collapsed).toBe(true);
  });

  it('persists the new state on toggle', async () => {
    const { result } = renderHook(() => useSectionCollapse('history.headToHead'));
    await waitFor(() => expect(mockedStorage.getItem).toHaveBeenCalled());

    act(() => result.current.toggle());

    expect(result.current.collapsed).toBe(true);
    expect(mockedStorage.setItem).toHaveBeenCalledWith(
      'section-collapsed:history.headToHead',
      'true');

    act(() => result.current.toggle());

    expect(result.current.collapsed).toBe(false);
    expect(mockedStorage.setItem).toHaveBeenLastCalledWith(
      'section-collapsed:history.headToHead',
      'false');
  });

  it('keys sections separately so collapsing one leaves the other alone', async () => {
    mockedStorage.getItem.mockImplementation((key: string) =>
      Promise.resolve(key === 'section-collapsed:history.lastSeason' ? 'true' : null));

    const h2h = renderHook(() => useSectionCollapse('history.headToHead'));
    const lastSeason = renderHook(() => useSectionCollapse('history.lastSeason'));

    await waitFor(() => expect(lastSeason.result.current.collapsed).toBe(true));
    expect(h2h.result.current.collapsed).toBe(false);
  });

  it('reads storage once per section, not once per mounted card', async () => {
    // A slate can render dozens of cards; each one hydrating independently
    // would be dozens of redundant reads of the same key.
    renderHook(() => useSectionCollapse('history.headToHead'));
    renderHook(() => useSectionCollapse('history.headToHead'));
    renderHook(() => useSectionCollapse('history.headToHead'));

    await waitFor(() => expect(mockedStorage.getItem).toHaveBeenCalled());
    expect(mockedStorage.getItem).toHaveBeenCalledTimes(1);
  });

  it('does not let a slow read clobber a toggle made while it was in flight', async () => {
    let resolveRead: (v: string | null) => void = () => {};
    mockedStorage.getItem.mockReturnValue(
      new Promise<string | null>((resolve) => { resolveRead = resolve; }));

    const { result } = renderHook(() => useSectionCollapse('history.headToHead'));

    act(() => result.current.toggle());
    expect(result.current.collapsed).toBe(true);

    // The stored value lands late and disagrees — the user's choice wins.
    await act(async () => { resolveRead(null); });

    expect(result.current.collapsed).toBe(true);
  });

  it('falls back to expanded when storage is unreadable', async () => {
    // A read failure must not hide content — degrade toward showing more.
    mockedStorage.getItem.mockRejectedValue(new Error('storage unavailable'));

    const { result } = renderHook(() => useSectionCollapse('history.headToHead'));

    await waitFor(() => expect(mockedStorage.getItem).toHaveBeenCalled());
    expect(result.current.collapsed).toBe(false);
  });
});
