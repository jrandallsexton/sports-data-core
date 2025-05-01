import { useEffect } from "react";
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
import { ThemeProvider } from "./contexts/ThemeContext";
import PrivateRoute from "./routes/PrivateRoute";
import { AuthProvider } from "./contexts/AuthContext";

function AppRoutes() {
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    const token = localStorage.getItem("authToken");
    if (token && location.pathname === "/") {
      navigate("/app");
    }
  }, [location, navigate]);

  return (
    <>
      <Toaster position="top-center" reverseOrder={false} />
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
    </>
  );
}

function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <Router>
          <AppRoutes />
        </Router>
      </AuthProvider>
    </ThemeProvider>
  );
}

export default App;
