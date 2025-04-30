// src/api/usersApi.js
import apiClient from "./apiClient";

const UsersApi = {
  createOrUpdateUser: (userData) =>
    apiClient.post("/users", userData),
};

export default UsersApi;
