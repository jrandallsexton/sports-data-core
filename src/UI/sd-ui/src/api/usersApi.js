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
    apiClient.patch("/user/me/notification-preferences", prefs),
  // Typed per-user options (UserOptionsDto). GET returns defaults when the
  // user has never changed anything; PATCH is a full replacement of KNOWN
  // options (unknown/newer options are never touched server-side).
  // See docs/features/user-options.md.
  updateUserOptions: (options) => apiClient.patch("/user/me/options", options),
  // DELETE /user/me — server anonymizes the record and removes the Firebase
  // login; caller signs out afterward. Mirrors the mobile app's deleteAccount.
  deleteAccount: () => apiClient.delete("/user/me")
};

export default UsersApi;
