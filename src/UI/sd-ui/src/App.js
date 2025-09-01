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
import LandingPage from "./components/landing/LandingPage";
import TermsPage from "./components/legal/TermsPage";
import PrivacyPage from "./components/legal/PrivacyPage";
import ErrorPage from "components/common/ErrorPage"; // ✅ reusable component
import { ThemeProvider } from "./contexts/ThemeContext";
import PrivateRoute from "./routes/PrivateRoute";
import { AuthProvider, useAuth } from "./contexts/AuthContext";
import { UserProvider } from "./contexts/UserContext";
import { LeagueProvider } from "./contexts/LeagueContext";
import { setGlobalApiErrorHandler } from "api/apiClient";

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
      {apiOffline ? (
        <ErrorPage message="We lost the ball trying to contact the server." />
      ) : (
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/signup" element={<SignupPage />} />
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
            <Router>
              <AppRoutes />
            </Router>
          </LeagueProvider>
        </UserProvider>
      </AuthProvider>
    </ThemeProvider>
  );
}

export default App;
