import axios from "axios";
import { getAuth } from "firebase/auth";

let onGlobalApiError = null;

export function setGlobalApiErrorHandler(handler) {
  onGlobalApiError = handler;
}

const apiClient = axios.create({
  baseURL: process.env.REACT_APP_API_BASE_URL,
  timeout: 10000,
  headers: {
    "Content-Type": "application/json",
  },
  // No cookies needed
  withCredentials: false,
});

apiClient.interceptors.request.use(async (config) => {
  const auth = getAuth();
  const user = auth.currentUser;

  if (user) {
    const token = await user.getIdToken();
    config.headers.Authorization = `Bearer ${token}`;
  } else {
    console.warn("No Firebase user found. Skipping Authorization header.");
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => {
    console.log("API Response:", response.status, response.config.url);
    return response;
  },
  async (error) => {
    console.log("API Error:", error.response?.status, error.config?.url);

    if (error.response?.status === 401) {
      error.isUnauthorized = true;
    }

    if (!error.response && onGlobalApiError) {
      // Network error / timeout / CORS failure, etc.
      onGlobalApiError(error);
    }

    return Promise.reject(error);
  }
);

export default apiClient;
