import apiClient from './apiClient';

const AdminApi = {
  getCompetitionsWithoutCompetitors: () =>
    // Controller likely under /ui/admin/errors/..., use /ui/admin as a safe prefix
    apiClient.get('/admin/errors/competitions-without-competitors'),
  getCompetitionsWithoutPlays: () =>
    apiClient.get('/admin/errors/competitions-without-plays'),
  getCompetitionsWithoutDrives: () =>
    apiClient.get('/admin/errors/competitions-without-drives'),
  resetPreview: (contestId) =>
    apiClient.post(`/admin/matchup/preview/${contestId}/reset`),
};

export default AdminApi;
