
import apiClient from "./apiClient";

const UsersApi = {
  createOrUpdateUser: (userData) =>
    apiClient.post("/user", userData),
};

export default UsersApi;
