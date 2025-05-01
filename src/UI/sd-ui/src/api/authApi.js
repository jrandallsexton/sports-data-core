
import apiClient from "./apiClient";

const AuthApi = {
  validateToken: () => apiClient.get("/auth/claims"),
};

export default AuthApi;
