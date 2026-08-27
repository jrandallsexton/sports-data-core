import axios, { type AxiosInstance } from 'axios';
import { getAuth, onAuthStateChanged } from 'firebase/auth';

const BASE_URL =
  process.env.EXPO_PUBLIC_API_BASE_URL ?? 'https://api.sportdeets.com';

export const apiClient: AxiosInstance = axios.create({
  baseURL: BASE_URL,
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
});

// Resolves once Firebase has determined the initial auth state (persisted
// session restored or confirmed absent). Requests fired during app boot
// used to race this: `getAuth().currentUser` is null until the first
// onAuthStateChanged emission, so early queries went out with NO bearer
// and 401'd (creation-availability, invitations, discover, rankings),
// then refetched after auth landed. Gating on INITIALIZATION — not on
// being signed in — keeps anonymous endpoints working for logged-out
// users while eliminating the race for everyone else. Lazy so module
// import order can't touch Firebase before the app initializes it.
let authInitialized: Promise<void> | null = null;
const waitForAuthInit = (): Promise<void> => {
  authInitialized ??= new Promise((resolve) => {
    const unsubscribe = onAuthStateChanged(getAuth(), () => {
      unsubscribe();
      resolve();
    });
  });
  return authInitialized;
};

// Attach Firebase JWT on every request
apiClient.interceptors.request.use(async (config) => {
  await waitForAuthInit();
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
