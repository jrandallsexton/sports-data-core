import apiClient from "./apiClient";

const UsersApi = {
  createOrUpdateUser: (userData) => apiClient.post("/user", userData),
  getCurrentUser: () => apiClient.get("/user/me")
};

export default UsersApi;
