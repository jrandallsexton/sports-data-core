import apiClient from "./apiClient";

// No setToken: the web client is header-based (apiClient attaches a
// per-request bearer token, withCredentials: false), so the server-side
// cookie exchange has no consumer. clearToken is retained — sign-out and
// account deletion still call it to clear any legacy cookie.
const AuthApi = {
  validateToken: () => apiClient.get("/auth/claims"),
  clearToken: () => apiClient.post("/auth/clear-token")
};

export default AuthApi;
