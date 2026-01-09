# Token Expiration Issue Analysis

**Date:** January 8, 2026  
**Issue:** Users see error page ("We lost the ball trying to contact the server") when session expires instead of automatic refresh/re-authentication

**CRITICAL FINDING:** This occurs in **~10 minutes**, NOT 1 hour (Firebase token lifetime). This indicates the issue is NOT normal token expiration.

## Problem Summary

When users step away from the application and return (often in **as little as 10 minutes**), **XHR requests show as "canceled" in browser dev tools** instead of completing with 401 errors. Instead of handling this gracefully by:
1. Refreshing the token automatically
2. Redirecting to login
3. Showing a user-friendly message

The application shows a generic error page suggesting the API is offline.

**Critical Finding:** Requests are being **canceled**, not rejected with 401. This indicates:
- Firebase may be signing out the user when token expires
- `auth.currentUser` becomes `null` mid-flight
- Pending requests are cancelled when `getIdToken()` fails
- The request interceptor fails silently without Authorization header

## Current Authentication Flow

### Frontend (UI)

#### 1. Token Acquisition ([apiClient.js](../src/UI/sd-ui/src/api/apiClient.js))
```javascript
apiClient.interceptors.request.use(async (config) => {
  const auth = getAuth();
  const user = auth.currentUser;

  if (user) {
    try {
      // Get fresh token - Firebase handles caching automatically
      const token = await user.getIdToken(false); // ⚠️ false = uses cached token
      config.headers.Authorization = `Bearer ${token}`;
    } catch (error) {
      console.error('Failed to get Firebase token:', error);
      // Don't block the request, let the API return 401 if needed
    }
  }

  return config;
});
```

**Issue:** `getIdToken(false)` uses cached token which may be expired.

#### 2. Token Refresh ([AuthContext.jsx](../src/UI/sd-ui/src/contexts/AuthContext.jsx))
```javascript
// Refresh token every 50 minutes (tokens expire after 1 hour)
useEffect(() => {
  if (!user) return;

  const refreshInterval = setInterval(async () => {
    console.log('Forcing Firebase token refresh...');
    try {
      await user.getIdToken(true); // Force refresh
      console.log('Token refreshed successfully');
    } catch (error) {
      console.error('Token refresh failed:', error);
    }
  }, 50 * 60 * 1000); // 50 minutes

  return () => clearInterval(refreshInterval);
}, [user]);
```

**Issue:** This only refreshes if the user is actively on the page. If the user:
- Closes the browser tab
- Suspends their laptop
- Steps away for >60 minutes

The interval doesn't run, and the token expires.

#### 3. Error Handling ([apiClient.js](../src/UI/sd-ui/src/api/apiClient.js))
```javascript
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
      // ⚠️ No handling - just logs and rejects
    }

    // Network error / timeout / CORS failure
    if (!error.response && onGlobalApiError) {
      console.error('API offline or unreachable', error.message);
      onGlobalApiError(error); // ⚠️ Shows "API offline" error page
    }

    return Promise.reject(error);
  }
);
```

**Issues:**
1. 401 errors are logged but not handled - no retry with fresh token
2. Missing retry logic after token refresh
3. Network errors trigger "API offline" error page ([MainApp.jsx:119](../src/UI/sd-ui/src/MainApp.jsx#L119))

### Backend (API)

#### JWT Configuration ([Program.cs](../src/SportsData.Api/Program.cs#L47-L94))
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = "https://securetoken.google.com/sportdeets-dev",
    ValidateAudience = true,
    ValidAudience = "sportdeets-dev",
    ValidateLifetime = true, // ⚠️ Validates token expiration
    NameClaimType = "user_id",
    RoleClaimType = "role"
};
```

**Behavior:** When an expired token is received, JWT middleware returns 401 Unauthorized.

#### Firebase Middleware ([FirebaseAuthenticationMiddleware.cs](../src/SportsData.Api/Middleware/FirebaseAuthenticationMiddleware.cs))
```csharp
if (!string.IsNullOrEmpty(token))
{
    FirebaseToken decodedToken;
    try
    {
        decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Firebase token validation failed");
        context.Response.StatusCode = 401; // ⚠️ Returns 401
        return;
    }
    // ...
}
```

**Behavior:** Validates token server-side. If expired, returns 401.

## Root Causes

### 1. **Premature Session Loss (PRIMARY ISSUE - 10 Minutes, NOT 1 Hour)**

**CRITICAL:** Firebase ID tokens are valid for **1 hour**, but sessions are being lost in **~10 minutes**. This indicates:

**Possible Causes:**
1. **Browser tab suspension/hibernation**
   - Modern browsers aggressively suspend inactive tabs
   - Firebase auth state may not survive suspension
   - `onAuthStateChanged` may fire with `null` when tab resumes

2. **Firebase session persistence failure**
   - No explicit `setPersistence()` configuration
   - Default LOCAL persistence may not be working correctly
   - IndexedDB/localStorage issues in the browser

3. **Network connectivity loss during inactivity**
   - Firebase SDK loses connection
   - Fails to re-establish session on reconnect
   - Clears auth state as "invalid"

4. **Memory pressure/browser resource management**
   - Browser may clear auth state to free memory
   - Happens faster on mobile/low-memory devices

5. **Service Worker or extension interference**
   - Ad blockers, privacy extensions
   - May block Firebase auth state persistence
   - Could explain intermittent behavior

**Evidence:**
- Occurs in 10 minutes (not 1 hour token expiry)
- Requests show "canceled" (auth.currentUser becomes null)
- No 401 errors from backend (requests never reach server)

### 2. **Firebase Auto-Logout When Session Lost**
When Firebase session is lost (for any of the above reasons):
- Firebase SDK may clear `auth.currentUser` (sets to `null`)
- `onAuthStateChanged` fires with `null` user
- AuthContext updates: `setUser(null)`
- **All in-flight API requests are CANCELED** because:
  - Request interceptor checks `auth.currentUser` 
  - If `null`, no Authorization header is added
  - Axios may cancel the request as invalid

**Evidence from code:**
```javascript
// apiClient.js - Request Interceptor
const user = auth.currentUser; // ⚠️ Becomes null when token expires

if (user) {
  try {
    const token = await user.getIdToken(false);
    config.headers.Authorization = `Bearer ${token}`;
  } catch (error) {
    console.error('Failed to get Firebase token:', error);
    // ⚠️ Silent failure - request proceeds WITHOUT auth header
  }
}
// ⚠️ If user is null, request continues with NO Authorization header
```

### 2. **Silent Failure in Request In (CRITICAL)**
Firebase auth has no explicit persistence configuration:
```javascript
// firebase.js
const firebaseApp = initializeApp(firebaseConfig);
const auth = getAuth(firebaseApp);
// ⚠️ No setPersistence() call - uses default LOCAL persistence
// ⚠️ No error handling if persistence fails
// ⚠️ No verification that persistence is working
```

**This is likely the root cause!** Without explicit persistence:
- May fall back to SESSION or NONE in some browsers
- IndexedDB issues could cause silent failures
- Tab suspension may clear non-persistent state
- No way to detect or recover from persistence failures

### 7. **No Session Recovery on Tab Resume**
When browser tab resumes from suspension:
- No code to verify auth state is still valid
- No code to re-authenticate if session was lost
- `onAuthStateChanged` may fire with `null` but app doesn't handle it
- User is unknowingly signed out

### 8. **Dual Auth Approach (Not Currently Used)**
**Backend supports BOTH:**
```csharp
// Program.cs - JWT Bearer config
options.Events = new JwtBearerEvents {
  OnMessageReceived = context => {
    // Tries to get token from cookie as fallback
    context.Token = context.Request.Cookies["authToken"];
  }
};
```

**Frontend only uses Authorization header:**
```javascript
const apiClient = axios.create({
  withCredentials: false, // ⚠️ Not sending cookies
});
config.headers.Authorization = `Bearer ${token}`; // Only this is used
```

The `/auth/set-token` and `/auth/clear-token` endpoints exist but are **not being called** in the current auth flow. This creates an inconsistency where the backend is prepared to handle cookies, but the frontend never sends them. This is NOT causing the issue, but shows there may have been an incomplete migration from cookie-based to header-based auth.
### 3. **No Token Validation Before Use**
`getIdToken(false)` returns cached token without checking expiration:
```javascript
const token = await user.getIdToken(false); // Uses cache, may be expired
```
If the token is expired and Firebase has already invalidated the session, this fails silently.

### 4. **Interval-Based Refresh Unreliable**
The 50-minute refresh interval doesn't run when:
- Browser tab is suspended (most common)
- Device sleeps/suspends
- Browser is closed
- User navigates away from page

```javascript
// AuthContext.jsx
const refreshInterval = setInterval(async () => {
  await user.getIdToken(true); // Never runs if tab suspended
}, 50 * 60 * 1000);
```

### 5. **Cascade Effect from Auth State Change**
When `onAuthStateChanged` fires with `null`:
1. `AuthContext` sets `user = null`
2. `UserContext` sees `!user` and sets `userDto = null`
3. Components re-render with no user
4. Pending API requests are canceled mid-flight
5. User sees "API offline" error page

### 6. **No Persistence Configuration**
Firebase auth has no explicit persistence configuration:
```javascript
// firebase.js
const firebaseApp = initializeApp(firebaseConfig);
const auth = getAuth(firebaseApp);
// ⚠️ No setPersistence() call - uses default LOCAL persistence
```

However, even with LOCAL persistence, the token itself expires and Firebase may sign out the user.

## Recommended Solutions

### Solution 1: Implement Automatic Token Refresh on 401 (Recommended)

**A. Fix Request Interceptor to Validate Token Before Use**

Update `apiClient.js` request interceptor to check token expiration and refresh proactively:

```javascript
apiClient.interceptors.request.use(async (config) => {
  const auth = getAuth();
  const user = auth.currentUser;

  if (user) {
    try {
      // Check if token is about to expire
      const tokenResult = await user.getIdTokenResult(false);
      const expirationTime = new Date(tokenResult.expirationTime).getTime();
      const now = Date.now();
      const fiveMinutes = 5 * 60 * 1000;
      
      let token;
      if (expirationTime - now < fiveMinutes) {
        // Token expires soon or expired, force refresh
        console.log('Token expiring soon, forcing refresh...');
        token = await user.getIdToken(true);
      } else {
        token = tokenResult.token;
      }
      
      config.headers.Authorization = `Bearer ${token}`;
    } catch (error) {
      console.error('Failed to get Firebase token:', error);
      
      // ⚠️ CRITICAL: If token fetch fails, reject the request
      // Don't let it proceed without authentication
      return Promise.reject({
        ...error,
        message: 'Authentication required - token expired',
        config
      });
    }
  } else {
    // No user - reject request immediately
    console.warn('No authenticated user - canceling request');
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
```

**B. Add Response Interceptor for 401 Handling**

```javascript
apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;
    const status = error.response?.status;

    // Handle authentication errors
    if (error.isAuthError) {
      console.error('Authentication error - redirecting to login');
      window.location.href = '/';
      return Promise.reject(error);
    }

    // Handle 401 Unauthorized from API
    if (status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      try {
        const auth = getAuth();
        const user = auth.currentUser;

        if (user) {
          console.log('401 received, attempting token refresh...');
          
          // Force token refresh
          const newToken = await user.getIdToken(true);
          
          // Update request with new token
          originalRequest.headers.Authorization = `Bearer ${newToken}`;
          
          // Retry original request
          return apiClient(originalRequest);
        } else {
          // No user logged in - redirect to login
          console.warn('No user found after 401, redirecting to login');
          window.location.href = '/';
          return Promise.reject(error);
        }
      } catch (refreshError) {
        console.error('Token refresh failed:', refreshError);
        
        // Refresh failed - sign out and redirect
        const auth = getAuth();
        await auth.signOut();
        window.location.href = '/';
        
        return Promise.reject(refreshError);
      }
    }

    // Network error / timeout - but NOT auth errors
    if (!error.response && !error.isAuthError && onGlobalApiError) {
      console.error('API offline or unreachable', error.message);
      onGlobalApiError(error);
    }

    return Promise.reject(error);
  }
);
```

**Benefits:**
- Prevents requests from being sent without auth headers
- Proactively refreshes tokens before expiration
- Rejects auth-failed requests cleanly (no "canceled" status)
- Distinguishes between auth errors and network errors
- Automatic retry on 401 with refreshed token
- Graceful redirect to login if refresh fails

### Solution 2: Always Request Fresh Tokens

Update token acquisition to always verify freshness:

```javascript
apiClient.interceptors.request.use(async (config) => {
  const auth = getAuth();
  const user = auth.currentUser;

  if (user) {
    try {
      // Option A: Always force refresh (expensive)
      // const token = await user.getIdToken(true);
      
      // Option B: Let Firebase SDK handle it (checks expiry internally)
      const token = await user.getIdToken(false);
      
      // Option C: Check expiry and refresh if needed
      const tokenResult = await user.getIdTokenResult(false);
      const expirationTime = new Date(tokenResult.expirationTime).getTime();
      const now = Date.now();
      const fiveMinutes = 5 * 60 * 1000;
      
      let token;
      if (expirationTime - now < fiveMinutes) {
        // Token expires in <5 min, refresh it
        token = await user.getIdToken(true);
      } else {
        token = tokenResult.token;
      }
      
      config.headers.Authorization = `Bearer ${token}`;
    } catch (error) {
      console.error('Failed to get Firebase token:', error);
    }
  }

  return config;
});
```

### Solution 3: Remove Unreliable Interval-Based Refresh

The current interval in `AuthContext.jsx` should be removed or replaced with a more reliable approach:

```javascript
// REMOVE THIS:
useEffect(() => {
  if (!user) return;

  const refreshInterval = setInterval(async () => {
    console.log('Forcing Firebase token refresh...');
    try {
      await user.getIdToken(true);
      console.log('Token refreshed successfully');
    } catch (error) {
      console.error('Token refresh failed:', error);
    }
  }, 50 * 60 * 1000); // 50 minutes

  return () => clearInterval(refreshInterval);
}, [user]);
```

**Rationale:** 
- Firebase SDK already handles token refresh automatically
- Checking on each request (Solution 2 Option C) is more reliable
- Interval doesn't account for browser suspension/closure

### Solution 4: Improve Error Messaging

Differentiate between token expiration and actual API offline:

```javascript
apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const status = error.response?.status;
    
    if (status === 401) {
      // Try refresh (see Solution 1)
      // If refresh fails, show auth-specific message
      console.error('Authentication failed - token expired or invalid');
      // Don't trigger "API offline" message
    } else if (!error.response) {
      // Actual network error
      onGlobalApiError(error);
    }
    
    return Promise.reject(error);
  }
);
```

## Implementation Plan

### Phase 0: Fix Root Cause (IMMEDIATE - Highest Priority)
1. ✅ Add explicit `setPersistence(auth, browserLocalPersistence)` to firebase.js
2. ✅ Add error handling for persistence failures
3. ✅ Add visibility change listener to detect/recover from tab suspension
4. ✅ Test with browser tab suspension (10-15 minutes)
5. ✅ Verify session survives tab suspension

### Phase 1: Defensive Fixes (Immediate)
1. ✅ Implement Solution 1: Fix request interceptor to reject auth-less requests
2. ✅ Update error handling to not treat auth errors as "API offline"
3. ✅ Add token refresh on 401 responses

### Phase 2: Optimization (Short-term)
1. ✅ Implement Solution 2 Option C: Proactive token refresh before expiry
2. ✅ Remove unreliable interval-based refresh from AuthContext
3. ✅ Add logging for token refresh events

### Phase 3: Enhanced UX (Medium-term)
1. ✅ Add loading indicator during token refresh
2. ✅ Show toast notification if refresh fails: "Your session expired. Please log in again."
3. ✅ Implement session persistence across browser refreshes

### Phase 4: Monitoring (Long-term)
1. ✅ Browser Tab Suspension (MOST IMPORTANT - Reproduces the Issue)
1. **Token Expiration Simulation:**
   - Login to app
   - Wait 61+ minutes (or manually set system clock forward)
   - Attempt API call
   - Verify: Token refreshes automatically, request succeeds
Background Tab Test:**
   - Login to app
   - Switch to another tab
   - Wait 15-20 minutes
   - Switch back and attempt API call
   - **Current behavior:** Requests show "canceled"
   - **Expected after fix:** Session persists, requests succeed

3. **Device Suspend Test:**
   - Login to app
   - Close laptop lid / lock phone
   - Wait 15 minutes
   - Resume device and return to app
   - **Current behavior:** User signed out, "API offline" error
   - **Expected after fix:** Session persists, or graceful re-login prompt

4. **Token Expiration Simulation:**
   - Login to app
   - Wait 61+ minutes (or manually set system clock forward)
   - Attempt API call
   - Verify: Token refreshes automatically, request succeeds

5
3. **Invalid Token:**
   - Login to app
   - Manually corrupt auth token in browser dev tools
   - Attempt API call
   - Verify: Redirects to login page with appropriate message

### Automated Testing
```javascript
// Test: Token refresh on 401
it('should refresh token and retry request on 401', async () => {
  const mockUser = {
    getIdToken: jest.fn()
      .mockResolvedValueOnce('expired-token')
      .mockResolvedValueOnce('fresh-token')
  };
  
  getAuth.mockReturnValue({ currentUser: mockUser });
  
  // Mock API returning 401 first, then 200
  mock.onGet('/api/test')
    .replyOnce(401)
    .onGet('/api/test').replyOnce(200, { success: true });
  
  const response = await apiClient.get('/api/test');
  
  expect(mockUser.getIdToken).toHaveBeenCalledWith(true); // Force refresh
  expect(response.data).toEqual({ success: true });
});
```

## Related Files

### Frontend
- [apiClient.js](../src/UI/sd-ui/src/api/apiClient.js) - Axios configuration and interceptors
- [AuthContext.jsx& Session Lifecycle

1. **Token Lifetime:** 1 hour (3600 seconds)
2. **Auto-Refresh:** Firebase SDK automatically refreshes 5 minutes before expiry (if app is active)
3. **Cached Access:** `getIdToken(false)` returns cached token (may be expired if app was inactive)
4. **Force Refresh:** `getIdToken(true)` always fetches fresh token from Firebase

## Browser Tab Suspension Behavior

**Modern browsers aggressively suspend inactive tabs:**
- **Chrome:** Suspends tabs after 5-10 minutes of inactivity
- **Safari:** Aggressive tab suspension on mobile, moderate on desktop
- **Firefox:** Less aggressive but still suspends background tabs
- **Edge:** Similar to Chrome (Chromium-based)

**During suspension:**
- JavaScript execution stops (intervals don't run)
- Network connections may be dropped
- IndexedDB/localStorage access is suspended
- WebSocket connections are closed
- Firebase auth state may be cleared from memory

**On resume:**
- `visibilitychange` event fires
- JavaScript resumes execution
- Firebase SDK attempts to restore auth state from persistence
- **If persistence failed or wasn't configured:** `auth.currentUser` is `null`

**This explains the 10-minute timeframe!** It's not token expiry; it's tab suspension clearing the in-memory auth state when persistence isn't properly configured.
- [FirebaseAuthenticationMiddleware.cs](../src/SportsData.Api/Middleware/FirebaseAuthenticationMiddleware.cs) - Token validation
- [AuthController.cs](../src/SportsData.Api/Application/Auth/AuthController.cs) - Auth endpoints

## Firebase Token Lifecycle

1. **Token Lifetime:** 1 hour (3600 seconds)
2. **Auto-Refresh:** Firebase SDK automatically refreshes 5 minutes before expiry (if app is active)
3. **Cached Access:** `getIdToken(false)` returns cached token (may be expired if app was inactive)
4. **Force Refresh:** `getIdToken(true)` always fetches fresh token from Firebase

## References

- [Firebase Auth REST API](https://firebase.google.com/docs/reference/rest/auth)
- [Firebase ID Tokens](https://firebase.google.com/docs/auth/admin/verify-id-tokens)
- [Axios Interceptors](https://axios-http.com/docs/interceptors)
- [JWT Token Refresh Patterns](https://auth0.com/blog/refresh-tokens-what-are-they-and-when-to-use-them/)

---

**Status:** Analysis Complete - Ready for Implementation
