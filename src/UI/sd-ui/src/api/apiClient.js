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
      // Check if token is about to expire and refresh proactively
      const tokenResult = await user.getIdTokenResult(false);
      const expirationTime = new Date(tokenResult.expirationTime).getTime();
      const now = Date.now();
      const fiveMinutes = 5 * 60 * 1000;
      
      let token;
      if (expirationTime - now < fiveMinutes) {
        // Token expires soon or already expired, force refresh
        console.log('⏰ Token expiring soon, forcing refresh...');
        token = await user.getIdToken(true);
        console.log('✅ Token refreshed successfully');
      } else {
        token = tokenResult.token;
      }
      
      config.headers.Authorization = `Bearer ${token}`;
    } catch (error) {
      console.error('❌ Failed to get Firebase token:', error);
      
      // CRITICAL: If token fetch fails, reject the request
      // Don't let it proceed without authentication
      return Promise.reject({
        ...error,
        message: 'Authentication required - unable to get valid token',
        config,
        isAuthError: true
      });
    }
  } else {
    // No authenticated user - reject request immediately
    // This prevents "canceled" requests in dev tools
    console.warn('⚠️ No authenticated user - rejecting API request');
    return Promise.reject({
      message: 'No authenticated user',
      config,
      isAuthError: true
    });
  }

  return config;
}, (error) => {
  // Handle request setup errors
  return Promise.reject(error);
});

apiClient.interceptors.response.use(
  (response) => {
    return response;
  },
  async (error) => {
    const originalRequest = error.config;
    const status = error.response?.status;
    const url = error.config?.url;
    
    console.log(`API Error: ${status || 'Network Error'} on ${url}`);

    // Handle authentication errors from request interceptor
    if (error.isAuthError) {
      console.error('🔒 Authentication error - user not logged in or token invalid');
      
      // Don't trigger "API offline" error for auth issues
      // Optionally redirect to login (or let app handle via AuthContext)
      if (window.location.pathname !== '/' && !window.location.pathname.startsWith('/login')) {
        console.warn('Redirecting to login due to auth error');
        window.location.href = '/';
      }
      
      return Promise.reject(error);
    }

    // Handle 401 Unauthorized from API (token was valid but expired/rejected by server)
    if (status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      try {
        const auth = getAuth();
        const user = auth.currentUser;

        if (user) {
          console.log('🔄 401 received, attempting token refresh...');
          
          // Force token refresh
          const newToken = await user.getIdToken(true);
          console.log('✅ Token refreshed, retrying request');
          
          // Update request with new token
          originalRequest.headers.Authorization = `Bearer ${newToken}`;
          
          // Retry original request
          return apiClient(originalRequest);
        } else {
          // No user logged in - redirect to login
          console.warn('❌ No user found after 401, redirecting to login');
          if (window.location.pathname !== '/') {
            window.location.href = '/';
          }
          return Promise.reject(error);
        }
      } catch (refreshError) {
        console.error('❌ Token refresh failed after 401:', refreshError);
        
        // Refresh failed - sign out and redirect
        const auth = getAuth();
        await auth.signOut();
        if (window.location.pathname !== '/') {
          window.location.href = '/';
        }
        
        return Promise.reject(refreshError);
      }
    } else if (status === 401) {
      // Already retried, still 401
      error.isUnauthorized = true;
      console.warn('⚠️ Still unauthorized after retry - user may need to re-authenticate');
    }

    // Network error / timeout / CORS failure (but NOT auth errors)
    if (!error.response && !error.isAuthError && onGlobalApiError) {
      console.error('🌐 API offline or unreachable', error.message);
      onGlobalApiError(error);
    }

    return Promise.reject(error);
  }
);

export default apiClient;
