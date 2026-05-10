import apiClient from './apiClient';

const AdminApi = {
  getCompetitionsWithoutCompetitors: () =>
    // Controller likely under /ui/admin/errors/..., use /ui/admin as a safe prefix
    apiClient.get('/admin/errors/competitions-without-competitors'),
  getCompetitionsWithoutPlays: () =>
    apiClient.get('/admin/errors/competitions-without-plays'),
  getCompetitionsWithoutDrives: () =>
    apiClient.get('/admin/errors/competitions-without-drives'),
  getCompetitionsWithoutMetrics: () =>
    apiClient.get('/admin/errors/competitions-without-metrics'),
  resetPreview: (contestId) =>
    apiClient.post(`/admin/matchup/preview/${contestId}/reset`),

  // Returns one MLB matchup in the same shape as the picks page so the
  // baseball SignalR debug page can render a real <MatchupCard /> for a
  // chosen contest. League-context fields (Predictions, AiWinner,
  // IsPreview*, HeadLine) come back null/empty per the endpoint contract.
  getBaseballMatchupForContest: (contestId) =>
    apiClient.get(`/admin/baseball/contests/${contestId}/matchup`),

  // Triggers a contest replay through the matching sport's Producer.
  // Producer enqueues the work and the bus emits ContestStatusChanged
  // once + a sport-specific *PlayCompleted per stored play. Use this
  // alongside the matchup card observer / debug card to verify the
  // SignalR pipeline end-to-end against a real game.
  replayBaseballContest: (contestId) =>
    apiClient.post(`/admin/baseball/contests/${contestId}/replay`),
  replayFootballContest: (contestId, league) =>
    apiClient.post(`/admin/football/contests/${contestId}/replay`, null, {
      params: league ? { league } : undefined,
    }),

  // SignalR debug harness — see docs/signalr-debug-harness-plan.md.
  // Each call publishes a synthetic integration event through API's
  // MassTransit + own consumer + SignalR fan-out, exercising the same
  // path a real Producer-originated event would. Server stamps the
  // ContestId so the client can't fan out for a real contest.
  // *PlayCompleted carries play description + scoreboard tick in one
  // event — there is no longer a separate play-completed broadcast.
  broadcastContestStatus: (payload) =>
    apiClient.post('/admin/signalr-debug/contest-status', payload),
  broadcastFootballPlay: (payload) =>
    apiClient.post('/admin/signalr-debug/football-play', payload),
  broadcastBaseballPlay: (payload) =>
    apiClient.post('/admin/signalr-debug/baseball-play', payload),
};

export default AdminApi;
