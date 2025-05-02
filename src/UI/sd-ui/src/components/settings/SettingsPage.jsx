import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTheme } from "../../contexts/ThemeContext";
import { useAuth } from "../../contexts/AuthContext";
import apiWrapper from "../../api/apiWrapper";
import "./SettingsPage.css";
import BadgesPanel from "../badges/BadgesPanel";

function SettingsPage() {
  const navigate = useNavigate();
  const { theme, toggleTheme } = useTheme();
  const { handleSignOut } = useAuth();
  const [user, setUser] = useState(null);
  const [error, setError] = useState("");

  useEffect(() => {
    const fetchUser = async () => {
      try {
        const response = await apiWrapper.Users.getCurrentUser();
        setUser(response.data);
      } catch (err) {
        console.error("Failed to load user:", err);
        if (err.isUnauthorized) {
          // Instead of signing out, just redirect to signup
          navigate('/signup', { replace: true });
          return;
        }
        setError("Could not fetch user settings.");
      }
    };

    fetchUser();
  }, [navigate]);

  return (
    <div className="settings-page">
      <h1>Settings</h1>

      {error && <p className="error">{error}</p>}

      <section className="settings-section">
        <h2>Profile</h2>
        <div className="settings-item">
          <span className="label">Email:</span>
          <span>{user?.email || "Loading..."}</span>
        </div>
        <div className="settings-item">
          <span className="label">Display Name:</span>
          <span>{user?.displayName || "Loading..."}</span>
        </div>
        <div className="settings-item">
          <span className="label">Timezone:</span>
          <span>{user?.timezone || "Loading..."}</span>
        </div>
      </section>

      <section className="settings-section">
        <h2>Theme</h2>
        <div className="settings-item">
          <span className="label">Current Theme:</span>
          <span>{theme}</span>
          <button className="toggle-theme-button" onClick={toggleTheme}>
            Toggle Theme
          </button>
        </div>
      </section>

      <section className="settings-section">
        <h2>Notifications</h2>
        <div className="settings-item">
          <span className="label">Email Alerts:</span>
          <input type="checkbox" disabled />
        </div>
        <div className="settings-item">
          <span className="label">Push Notifications:</span>
          <input type="checkbox" disabled />
        </div>
      </section>

      <BadgesPanel />
    </div>
  );
}

export default SettingsPage;
