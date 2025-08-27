import { useState } from "react";
import "./SystemNews.css";

function SystemNews() {
  const [visible, setVisible] = useState(() => {
    //localStorage.setItem("systemNewsDismissed", "false");
    return localStorage.getItem("systemNewsDismissed") !== "true";
  });

  const handleDismiss = () => {
    localStorage.setItem("systemNewsDismissed", "true");
    setVisible(false);
  };

  if (!visible) return null;

  return (
    <div className="card system-news-card">
      <button className="dismiss-button" onClick={handleDismiss}>
        ×
      </button>

      <h2>The Huddle</h2>
      <p>Welcome to <strong>sportDeets</strong>. We trash talk here. It's fun. Get over it.</p>

      <h3>✅ What Works</h3>
      <ul>
        <li>Create and join leagues</li>
        <li>Submit your picks (Straight Up or ATS)</li>
        <li>Read AI-powered matchup previews</li>
        <li>View standings (simulated until after Week 1)</li>
      </ul>

      <h3>⚠️ What’s Rough</h3>
      <ul>
        <li>No mobile app yet (2026 maybe)</li>
        <li>Picks are final once submitted</li>
        <li>You might break something — and that’s okay</li>
      </ul>

      <h3>🚧 What’s Coming</h3>
      <ul>
        <li>Weekly recaps and reminders</li>
        <li>Smack talk threads</li>
        <li>Game Center view for live updates</li>
        <li>More features once people start complaining</li>
      </ul>

      <p style={{ marginTop: "1rem" }}>
        Found a bug? Email <a href="mailto:help@sportdeets.com">help@sportdeets.com</a> — or just text me like a normal person.
      </p>
    </div>
  );
}

export default SystemNews;
