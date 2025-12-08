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
  withCredentials: false,
});

apiClient.interceptors.request.use(async (config) => {
  const auth = getAuth();
  const user = auth.currentUser;

  if (user) {
    try {
      // Get fresh token - Firebase handles caching automatically
      const token = await user.getIdToken(false);
      config.headers.Authorization = `Bearer ${token}`;
    } catch (error) {
      console.error('Failed to get Firebase token:', error);
      // Don't block the request, let the API return 401 if needed
    }
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => {
    return response;
  },
  async (error) => {
    const status = error.response?.status;
    const url = error.config?.url;
    
    console.log(`API Error: ${status || 'Network Error'} ${url}`);

    if (status === 401) {
      error.isUnauthorized = true;
      console.warn('Unauthorized request - user may need to re-authenticate');
    }

    // Network error / timeout / CORS failure
    if (!error.response && onGlobalApiError) {
      console.error('API offline or unreachable', error.message);
      onGlobalApiError(error);
    }

    return Promise.reject(error);
  }
);

export default apiClient;
