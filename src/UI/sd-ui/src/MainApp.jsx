import { Routes, Route, useNavigate } from "react-router-dom";
import { useEffect, useState } from "react";
import { getAuth, signOut } from "firebase/auth";
import { toast } from "react-hot-toast";
import Navigation from "./components/layout/Navigation";
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
  const [isSideNav, setIsSideNav] = useState(false);

  const handleSignOut = async () => {
    const auth = getAuth();
    try {
      await apiWrapper.Auth.clearToken();
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
      <Navigation 
        isSideNav={isSideNav} 
        onToggle={() => setIsSideNav(!isSideNav)}
        onSignOut={handleSignOutClick}
      />
      <main className={`main-content ${isSideNav ? 'side-nav-active' : ''}`}>
        <Routes>
          <Route index element={<HomePage />} />
          <Route path="picks" element={<PicksPage />} />
          <Route path="leaderboard" element={<LeaderboardPage />} />
          <Route path="messageboard" element={<MessageBoardPage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="team/:slug" element={<TeamCard />} />
        </Routes>
      </main>
    </div>
  );
}

export default MainApp;
