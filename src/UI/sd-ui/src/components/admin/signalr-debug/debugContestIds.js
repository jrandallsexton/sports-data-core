// Mirror of SignalRDebugContestIds in
// src/SportsData.Api/Application/Admin/SignalRDebug/SignalRDebugRequests.cs.
// The server stamps these onto outbound debug events so the client
// can't fan a debug payload out for a real contest. The widgets here
// subscribe to the same id via useContestUpdates so the round-trip
// re-renders the widget when the SignalR push arrives.
export const FOOTBALL_DEBUG_CONTEST_ID = 'aaaaaaaa-0000-0000-0000-000000000001';
export const BASEBALL_DEBUG_CONTEST_ID = 'aaaaaaaa-0000-0000-0000-000000000002';

// Sandbox franchise ids the football widget uses to flip possession.
// Picked deliberately fake so they never collide with real franchise
// ids in any context the widget might be merged with.
export const FOOTBALL_DEBUG_AWAY_ID = 'bbbbbbbb-0000-0000-0000-0000000000aa';
export const FOOTBALL_DEBUG_HOME_ID = 'bbbbbbbb-0000-0000-0000-0000000000bb';
