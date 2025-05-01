import React from "react";
import "./WelcomeDialog.css";

function WelcomeDialog({ onClose }) {
  return (
    <div className="welcome-overlay">
      <div className="welcome-dialog">
        <h2>Welcome to sportDeets!</h2>
        <p>This is where you'll make picks, see leaderboards, and stay in the game.</p>
        <button onClick={onClose}>Let’s Go!</button>
      </div>
    </div>
  );
}

export default WelcomeDialog;
