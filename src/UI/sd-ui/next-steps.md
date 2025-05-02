# Authentication System Improvements

## Frontend Changes (Completed)
- Updated `apiClient.js` to use cookies instead of localStorage
- Modified `AuthContext.jsx` for secure token management
- Updated `MainApp.jsx` sign-out handling
- Added `clearToken` method to `apiWrapper.js`

## Backend Changes (Pending)

### New Endpoints Required
1. **POST /api/auth/set-token**
   - Purpose: Sets the Firebase ID token in an HttpOnly cookie
   - Request Body: `{ token: string }` (the Firebase ID token)
   - Response: `{ success: boolean }`
   - Security: Should be called only after successful Firebase authentication

2. **POST /api/auth/clear-token**
   - Purpose: Clears the authentication cookie during sign-out
   - Request Body: None
   - Response: `{ success: boolean }`
   - Security: Should be accessible to authenticated users

3. **POST /api/auth/refresh-token** (optional, but recommended)
   - Purpose: Allows the frontend to request a new token when the current one is about to expire
   - Request Body: None (uses existing cookie)
   - Response: `{ token: string }` (new token)
   - Security: Should be accessible to authenticated users

### Backend Implementation Requirements

#### 1. Firebase Admin SDK Setup
- Install Firebase Admin SDK NuGet package
- Configure Firebase Admin SDK credentials
- Initialize Firebase Admin SDK in startup

#### 2. CORS and Cookie Policy Configuration
```csharp
// In Program.cs or Startup.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>())
               .AllowCredentials()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

// Configure cookie policy
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
});
```

#### 3. Token Validation Middleware
- Create middleware to validate Firebase tokens
- Skip validation for auth endpoints
- Handle unauthorized access appropriately

#### 4. Configuration Requirements
Add to `appsettings.json`:
```json
{
  "CookieDomain": "yourdomain.com",
  "AllowedOrigins": [
    "https://yourfrontenddomain.com"
  ]
}
```

## Security Improvements
1. **HttpOnly Cookies**
   - Tokens stored in HttpOnly cookies
   - Protected against XSS attacks
   - Inaccessible to JavaScript

2. **Automatic Token Refresh**
   - Tokens refreshed every 50 minutes
   - Prevents token expiration issues
   - Maintains session security

3. **Secure Cookie Settings**
   - `secure` flag in production
   - `sameSite: 'strict'` to prevent CSRF
   - Limited cookie lifetime

4. **Proper Cleanup**
   - Tokens properly cleared on sign-out
   - Interval cleanup on component unmount

5. **Error Handling**
   - Better error handling for token operations
   - Automatic redirect on unauthorized access

## Testing Requirements
1. Test authentication flow end-to-end
2. Verify token refresh mechanism
3. Test sign-out functionality
4. Verify unauthorized access handling
5. Test cross-origin requests
6. Verify cookie security settings

## Monitoring and Logging
1. Add logging for security events
2. Monitor token validation failures
3. Track authentication attempts
4. Log token refresh operations

## Documentation Updates
1. Update API documentation with new endpoints
2. Document security changes
3. Update deployment procedures
4. Document configuration requirements
