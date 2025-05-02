import { Routes, Route, NavLink, useNavigate } from "react-router-dom";
import { useEffect, useState } from "react";
import { getAuth, signOut } from "firebase/auth";
import { toast } from "react-hot-toast";
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
import TeamCard from "./components/teams/TeamCard";
import ConfirmationDialog from "./components/common/ConfirmationDialog";
import apiWrapper from "./api/apiWrapper";

function MainApp() {
  const navigate = useNavigate();
  const [showWelcome, setShowWelcome] = useState(false);
  const [showSignOutDialog, setShowSignOutDialog] = useState(false);

  const handleSignOut = async () => {
    const auth = getAuth();
    try {
      // Clear the token cookie first
      await apiWrapper.Auth.clearToken();
      
      // Then sign out from Firebase
      await signOut(auth);
      
      toast.success("Signed out successfully 👋");
      navigate("/");
    } catch (error) {
      console.error("Sign-out failed:", error);
      toast.error("Sign-out failed. Please try again.");
    }
  };

  const handleSignOutClick = () => {
    const dontAskAgain = localStorage.getItem("dontAskSignOut");
    if (dontAskAgain === "true") {
      handleSignOut();
    } else {
      setShowSignOutDialog(true);
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
      <ConfirmationDialog
        isOpen={showSignOutDialog}
        onClose={() => setShowSignOutDialog(false)}
        onConfirm={handleSignOut}
        title="Sign Out"
        message="Are you sure you want to sign out?"
        confirmText="Sign Out"
        storageKey="dontAskSignOut"
      />
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
          <button className="nav-link logout-button" onClick={handleSignOutClick}>
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
          <Route path="team/:slug" element={<TeamCard />} />
        </Routes>
      </main>
    </div>
  );
}

export default MainApp;
