// src/App.js
import { useState, useEffect } from "react";
import { Toaster } from "react-hot-toast";
import {
  BrowserRouter as Router,
  Routes,
  Route,
  useNavigate,
  useLocation,
} from "react-router-dom";
import "./App.css";

import MainApp from "./MainApp";
import SignupPage from "./components/signup/SignupPage";
import ForgotPassword from "./components/login/ForgotPassword";
import LandingPage from "./components/landing/LandingPage";
import TermsPage from "./components/legal/TermsPage";
import PrivacyPage from "./components/legal/PrivacyPage";
import AccountDeletionPage from "./components/legal/AccountDeletionPage";
import SupportPage from "./components/legal/SupportPage";
import ErrorPage from "components/common/ErrorPage"; // ✅ reusable component
import Gallery from "./components/gallery/Gallery";
import ResultsPage from "./components/results/ResultsPage";
import { ThemeProvider } from "./contexts/ThemeContext";
import PrivateRoute from "./routes/PrivateRoute";
import { AuthProvider, useAuth } from "./contexts/AuthContext";
import { UserProvider } from "./contexts/UserContext";
import { LeagueProvider } from "./contexts/LeagueContext";
import { ContestUpdatesProvider } from "./contexts/ContestUpdatesContext";
import { setGlobalApiErrorHandler } from "api/apiClient";
import MaintenancePage from "./components/maintenance/MaintenancePage";
import ScrollToTop from "./components/shared/ScrollToTop";

function AppRoutes() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, loading } = useAuth();
  const [apiOffline, setApiOffline] = useState(false);

  useEffect(() => {
    if (!loading && user && location.pathname === "/") {
      navigate("/app", { replace: true });
    }
  }, [location, navigate, user, loading]);

  useEffect(() => {
    if (!loading && user) {
      if (location.pathname === "/") {
        navigate("/app", { replace: true });
      } else if (location.pathname === "/signup") {
        navigate("/app", { replace: true });
      }
    }
  }, [location, navigate, user, loading]);

  useEffect(() => {
    setGlobalApiErrorHandler((err) => {
      console.warn("API offline or unreachable", err);
      setApiOffline(true);
    });
  }, []);

  if (loading) {
    return <div className="app-loading">Loading...</div>;
  }

  return (
    <>
      <Toaster position="top-center" reverseOrder={false} />
      {/* Static legal pages stay reachable during an API outage: they make no
          API calls, and /account-deletion is the URL submitted to Google
          Play's Data Safety section — a reviewer (or an uninstalled user)
          must never hit the offline error page there. /forgot-password belongs
          here for the same reason and one more: it talks only to Firebase, and
          an API outage is exactly when a locked-out user is most likely to be
          trying to get back in. All other routes keep the existing ErrorPage
          short-circuit. Trailing slashes are stripped before matching — the
          router itself treats /terms/ as /terms, so the allowlist must too. */}
      {apiOffline &&
      !["/terms", "/privacy", "/account-deletion", "/forgot-password"].includes(
        location.pathname.replace(/\/+$/, "") || "/"
      ) ? (
        <ErrorPage message="We lost the ball trying to contact the server." />
      ) : (
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/signup" element={<SignupPage />} />
          <Route path="/forgot-password" element={<ForgotPassword />} />
          <Route path="/gallery" element={<Gallery />} />
          <Route
            path="/results/:sport/:league/:seasonYear"
            element={<ResultsPage />}
          />
          <Route path="/maintenance" element={<MaintenancePage />} />
          <Route
            path="/app/*"
            element={
              <PrivateRoute>
                <MainApp />
              </PrivateRoute>
            }
          />
          <Route path="/terms" element={<TermsPage />} />
          <Route path="/privacy" element={<PrivacyPage />} />
          {/* Public account-deletion page — the URL submitted in Google
              Play's Data Safety section. Must stay reachable without auth. */}
          <Route path="/account-deletion" element={<AccountDeletionPage />} />
          {/* Public support page — the URL submitted as the Support URL in App
              Store Connect and the Play Console. Reviewers visit it, and a
              support URL that 404s is a rejection. Must stay reachable without
              auth: people who need help often cannot sign in. */}
          <Route path="/support" element={<SupportPage />} />
        </Routes>
      )}
    </>
  );
}

function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <UserProvider>
          <LeagueProvider>
            <ContestUpdatesProvider>
              <Router>
                <ScrollToTop />
                <AppRoutes />
              </Router>
            </ContestUpdatesProvider>
          </LeagueProvider>
        </UserProvider>
      </AuthProvider>
    </ThemeProvider>
  );
}

export default App;
