import * as signalR from '@microsoft/signalr';

export interface CreateSignalRConnectionOptions {
  /**
   * Base API URL. The hub is mounted at `/hubs/notifications` and that
   * suffix is appended internally so callers don't have to remember it.
   */
  url: string;
  /**
   * Returns a Firebase ID token. SignalR invokes this on the initial
   * negotiate and on every reconnect. Returning an empty string signals
   * "no token available" — the server will respond 401 and auto-reconnect
   * will retry, which is the desired behavior during a transient
   * sign-in / sign-out flip.
   */
  accessTokenFactory: () => Promise<string>;
}

/**
 * Pure factory — no React, no globals beyond the @microsoft/signalr
 * package. Kept separate from `useSignalRClient` so the hook can be tested
 * by injecting a fake builder.
 *
 * Reconnect policy: defaults from withAutomaticReconnect() are [0, 2000,
 * 10000, 30000] and then stop — matches the web hook's behavior.
 */
export function createSignalRConnection(
  options: CreateSignalRConnectionOptions,
): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${options.url}/hubs/notifications`, {
      accessTokenFactory: options.accessTokenFactory,
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();
}
