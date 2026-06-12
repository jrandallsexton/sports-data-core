// Public axios client for endpoints that don't require authentication.
//
// The main `apiClient` short-circuits every request when no Firebase user
// is present (request interceptor rejects with isAuthError → response
// interceptor redirects to "/"). That's correct behavior for the
// authenticated app, but it makes apiClient unusable for genuinely
// public endpoints (landing-page content, public results retrospective,
// public team pages, etc.).
//
// Use this client for any call whose controller is decorated with
// [AllowAnonymous] AND whose UI route is outside `<PrivateRoute>`. Keep
// the surface area small — every endpoint added here is one we promise
// is safe to expose without an auth token.
import axios from "axios";

const publicApiClient = axios.create({
  baseURL: process.env.REACT_APP_API_BASE_URL,
  timeout: 10000,
  headers: {
    "Content-Type": "application/json",
  },
  withCredentials: false,
});

export default publicApiClient;
