// src/api/usersApi.js
import apiClient from "./apiClient";

const UsersApi = {
  createOrUpdateUser: (userData) =>
    console.log("Creating or updating user:", userData) ||  // Debugging line
    apiClient.post("/api/user", userData),
};

export default UsersApi;
