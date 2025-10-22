import apiClient from './apiClient';

const AdminApi = {
  getCompetitionsWithoutCompetitors: () =>
    // Controller likely under /ui/admin/errors/..., use /ui/admin as a safe prefix
    apiClient.get('/admin/errors/competitions-without-competitors'),
};

export default AdminApi;
