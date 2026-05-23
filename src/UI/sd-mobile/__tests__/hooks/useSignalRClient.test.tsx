import React from 'react';
import { AppState } from 'react-native';
import { render, act } from '@testing-library/react-native';

import { useSignalRClient } from '@/src/hooks/useSignalRClient';
import { useContestUpdatesStore } from '@/src/stores/contestUpdatesStore';

// --- Mock @microsoft/signalr -------------------------------------------------
// The hook lives one level above the connection factory, but we mock the
// underlying signalR package here so the factory's HubConnectionBuilder chain
// returns a fake connection we can drive in tests.

type Handler = (...args: unknown[]) => void;

interface MockConnection {
  state: number;
  handlers: Map<string, Handler>;
  on: jest.Mock;
  start: jest.Mock;
  stop: jest.Mock;
  __invoke: (event: string, ...args: unknown[]) => void;
}

const HubConnectionState = {
  Disconnected: 0,
  Connecting: 1,
  Connected: 2,
  Disconnecting: 3,
  Reconnecting: 4,
};

let mockConnection: MockConnection;

const buildMockConnection = (): MockConnection => {
  const handlers = new Map<string, Handler>();
  const conn: Partial<MockConnection> = {
    state: HubConnectionState.Disconnected,
    handlers,
    on: jest.fn((name: string, handler: Handler) => {
      handlers.set(name, handler);
    }),
    __invoke: (event: string, ...args: unknown[]) => {
      const h = handlers.get(event);
      if (h) h(...args);
    },
  };
  // Arrow assignments (rather than `function(this: ...)`) so the closure
  // mutates the same `conn` object that callers hold a reference to.
  conn.start = jest.fn(() => {
    conn.state = HubConnectionState.Connected;
    return Promise.resolve();
  });
  conn.stop = jest.fn(() => {
    conn.state = HubConnectionState.Disconnected;
    return Promise.resolve();
  });
  return conn as MockConnection;
};

jest.mock('@microsoft/signalr', () => {
  return {
    __esModule: true,
    HubConnectionState,
    LogLevel: { Information: 'Information' },
    HubConnectionBuilder: jest.fn().mockImplementation(() => {
      const builder: Record<string, unknown> = {};
      builder.withUrl = jest.fn().mockReturnValue(builder);
      builder.withAutomaticReconnect = jest.fn().mockReturnValue(builder);
      builder.configureLogging = jest.fn().mockReturnValue(builder);
      builder.build = jest.fn().mockImplementation(() => {
        mockConnection = buildMockConnection();
        return mockConnection;
      });
      return builder;
    }),
  };
});

// --- Mock firebase/auth ------------------------------------------------------
jest.mock('firebase/auth', () => ({
  getAuth: () => ({
    currentUser: { getIdToken: async () => 'fake-token' },
  }),
}));

// --- AppState — spy on addEventListener so we can drive change events --------
let appStateListener: ((next: string) => void) | null = null;
let appStateSubscriptionRemove: jest.Mock | null = null;

// --- Test harness component --------------------------------------------------
function HookHarness(): null {
  useSignalRClient();
  return null;
}

describe('useSignalRClient', () => {
  beforeEach(() => {
    useContestUpdatesStore.setState(useContestUpdatesStore.getInitialState(), true);
    appStateListener = null;
    appStateSubscriptionRemove = jest.fn(() => {
      appStateListener = null;
    });
    jest.spyOn(AppState, 'addEventListener').mockImplementation((_event, listener) => {
      appStateListener = listener as (next: string) => void;
      return { remove: appStateSubscriptionRemove } as ReturnType<typeof AppState.addEventListener>;
    });
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('registers handlers for the three SignalR events', async () => {
    render(<HookHarness />);
    // Allow the start() promise to resolve.
    await act(async () => { await Promise.resolve(); });

    expect(mockConnection.on).toHaveBeenCalledWith('ContestStatusChanged', expect.any(Function));
    expect(mockConnection.on).toHaveBeenCalledWith('FootballPlayCompleted', expect.any(Function));
    expect(mockConnection.on).toHaveBeenCalledWith('BaseballPlayCompleted', expect.any(Function));
    expect(mockConnection.start).toHaveBeenCalledTimes(1);
  });

  it('dispatches incoming ContestStatusChanged into the store', async () => {
    render(<HookHarness />);
    await act(async () => { await Promise.resolve(); });

    const cid = '00000000-0000-0000-0000-000000000abc';
    act(() => {
      mockConnection.__invoke('ContestStatusChanged', { contestId: cid, status: 'STATUS_IN_PROGRESS', statusDescription: 'In Progress' });
    });

    expect(useContestUpdatesStore.getState().contests[cid]?.status).toBe('STATUS_IN_PROGRESS');
  });

  it('stops the connection when AppState transitions to background', async () => {
    render(<HookHarness />);
    await act(async () => { await Promise.resolve(); });

    expect(mockConnection.state).toBe(HubConnectionState.Connected);

    act(() => {
      appStateListener?.('background');
    });
    // The stop() call is async; flush.
    await act(async () => { await Promise.resolve(); });

    expect(mockConnection.stop).toHaveBeenCalled();
    expect(mockConnection.state).toBe(HubConnectionState.Disconnected);
  });

  it('restarts the connection when AppState returns to active', async () => {
    render(<HookHarness />);
    await act(async () => { await Promise.resolve(); });

    // Background, then foreground.
    act(() => { appStateListener?.('background'); });
    await act(async () => { await Promise.resolve(); });
    act(() => { appStateListener?.('active'); });
    await act(async () => { await Promise.resolve(); });

    // start() was called once on initial mount, once on foreground.
    expect(mockConnection.start).toHaveBeenCalledTimes(2);
  });

  it('stops the connection and removes the AppState subscription on unmount', async () => {
    const { unmount } = render(<HookHarness />);
    await act(async () => { await Promise.resolve(); });

    unmount();

    expect(mockConnection.stop).toHaveBeenCalled();
    expect(appStateSubscriptionRemove).toHaveBeenCalled();
  });
});
