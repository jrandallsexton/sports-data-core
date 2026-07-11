import apiClient from "./apiClient";

const UsersApi = {
  createOrUpdateUser: (userData) => apiClient.post("/user", userData),
  getCurrentUser: () => apiClient.get("/user/me"),
  updateTimezone: (timezone) => apiClient.patch("/user/me/timezone", { timezone }),
  updateUsername: (username) => apiClient.patch("/user/me/username", { username }),
  updateDisplayName: (displayName) => apiClient.patch("/user/me/displayname", { displayName }),
  // Per-category push-notification opt-in flags. GET returns all-on defaults
  // when the user has never changed a setting; PATCH is a full-set replacement.
  getNotificationPreferences: () => apiClient.get("/user/me/notification-preferences"),
  updateNotificationPreferences: (prefs) =>
    apiClient.patch("/user/me/notification-preferences", prefs)
};

export default UsersApi;
