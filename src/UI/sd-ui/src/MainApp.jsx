import { Routes, Route, NavLink, useNavigate } from "react-router-dom";
import { useEffect, useState } from "react";
import { getAuth, signOut } from "firebase/auth";
import {
  FaHome,
  FaFootballBall,
  FaTrophy,
  FaComments,
  FaCog,
  FaSignOutAlt,
} from "react-icons/fa";
import "./MainApp.css";

import PicksPage from "./components/picks/PicksPage.jsx";
import LeaderboardPage from "./components/leaderboard/LeaderboardPage.jsx";
import MessageBoardPage from "./components/messageboard/MessageboardPage.jsx";
import HomePage from "./components/home/HomePage.jsx";
import SettingsPage from "./components/settings/SettingsPage.jsx";
import WelcomeDialog from "./components/welcome/WelcomeDialog";

function MainApp() {
  const navigate = useNavigate();
  const [showWelcome, setShowWelcome] = useState(false);

  const handleSignOut = async () => {
    const auth = getAuth();
    try {
      await signOut(auth); // ✅ invalidate Firebase session
      localStorage.removeItem("authToken"); // ✅ remove stored token
      navigate("/"); // ✅ redirect to landing page
    } catch (error) {
      console.error("Sign-out failed:", error);
    }
  };

  useEffect(() => {
    const hasSeenWelcome = localStorage.getItem("seenWelcomeDialog");
    if (!hasSeenWelcome) {
      setShowWelcome(true);
      localStorage.setItem("seenWelcomeDialog", "true");
    }
  }, []);

  const handleWelcomeClose = () => {
    localStorage.setItem("seenWelcomeDialog", "true");
    setShowWelcome(false);
  };

  return (
    <div className="app-container">
      {showWelcome && <WelcomeDialog onClose={handleWelcomeClose} />}
      <aside className="sidebar">
        <h1 className="sidebar-title">sportDeets</h1>
        <nav className="nav-links">
          <NavLink to="/app/" end className="nav-link">
            <FaHome className="nav-icon" />
            <span>Home</span>
          </NavLink>
          <NavLink to="/app/picks" className="nav-link">
            <FaFootballBall className="nav-icon" />
            <span>Picks</span>
          </NavLink>
          <NavLink to="/app/leaderboard" className="nav-link">
            <FaTrophy className="nav-icon" />
            <span>Leaderboard</span>
          </NavLink>
          <NavLink to="/app/messageboard" className="nav-link">
            <FaComments className="nav-icon" />
            <span>Message Board</span>
          </NavLink>
          <NavLink to="/app/settings" className="nav-link">
            <FaCog className="nav-icon" />
            <span>Settings</span>
          </NavLink>
          <button className="nav-link logout-button" onClick={handleSignOut}>
            <FaSignOutAlt className="nav-icon" />
            <span>Sign Out</span>
          </button>
        </nav>
      </aside>

      <main className="main-content">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/picks" element={<PicksPage />} />
          <Route path="/leaderboard" element={<LeaderboardPage />} />
          <Route path="/messageboard" element={<MessageBoardPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </main>
    </div>
  );
}

export default MainApp;
