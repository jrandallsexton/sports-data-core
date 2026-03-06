import axios, { type AxiosInstance } from 'axios';
import { getAuth } from 'firebase/auth';

const BASE_URL =
  process.env.EXPO_PUBLIC_API_BASE_URL ?? 'https://api.sportdeets.com';

export const apiClient: AxiosInstance = axios.create({
  baseURL: BASE_URL,
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
});

// Attach Firebase JWT on every request
apiClient.interceptors.request.use(async (config) => {
  const user = getAuth().currentUser;
  if (user) {
    const token = await user.getIdToken();
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Surface 401s clearly; all other errors pass through as-is
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      error.isUnauthorized = true;
    }
    return Promise.reject(error);
  },
);
