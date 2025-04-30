// src/api/authApi.js
import apiClient from "./apiClient";

const AuthApi = {
  validateToken: () => apiClient.get("/api/auth/claims"),
};

export default AuthApi;
