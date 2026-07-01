import apiClient from "./apiClient";

const UsersApi = {
  createOrUpdateUser: (userData) => apiClient.post("/user", userData),
  getCurrentUser: () => apiClient.get("/user/me"),
  updateTimezone: (timezone) => apiClient.patch("/user/me/timezone", { timezone }),
  updateUsername: (username) => apiClient.patch("/user/me/username", { username }),
  updateDisplayName: (displayName) => apiClient.patch("/user/me/displayname", { displayName })
};

export default UsersApi;
