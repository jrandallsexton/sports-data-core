import apiClient from "./apiClient";

const AuthApi = {
  validateToken: () => apiClient.get("/auth/claims"),
  setToken: (token) => apiClient.post("/auth/set-token", { token }),
  clearToken: () => apiClient.post("/auth/clear-token")
};

export default AuthApi;
