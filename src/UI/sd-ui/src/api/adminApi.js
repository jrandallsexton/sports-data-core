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

  // SignalR debug harness — see docs/signalr-debug-harness-plan.md.
  // Each call publishes a synthetic integration event through API's
  // MassTransit + own consumer + SignalR fan-out, exercising the same
  // path a real Producer-originated event would. Server stamps the
  // ContestId so the client can't fan out for a real contest.
  broadcastContestStatus: (payload) =>
    apiClient.post('/admin/signalr-debug/contest-status', payload),
  broadcastFootballState: (payload) =>
    apiClient.post('/admin/signalr-debug/football-state', payload),
  broadcastBaseballState: (payload) =>
    apiClient.post('/admin/signalr-debug/baseball-state', payload),
  broadcastContestPlayCompleted: (payload) =>
    apiClient.post('/admin/signalr-debug/play-completed', payload),
};

export default AdminApi;
