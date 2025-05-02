import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTheme } from "../../contexts/ThemeContext";
import apiWrapper from "../../api/apiWrapper";
import "./SettingsPage.css";
import BadgesPanel from "../../components/badges/BadgesPanel.tsx";

function SettingsPage() {
  const navigate = useNavigate();
  const { theme, setTheme } = useTheme();
  const [user, setUser] = useState(null);
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    const fetchUser = async () => {
      try {
        const response = await apiWrapper.Users.getCurrentUser();
        if (isMounted) {
          setUser(response.data);
          setIsLoading(false);
        }
      } catch (err) {
        console.error("Failed to load user:", err);
        if (isMounted) {
          if (err.isUnauthorized) {
            // Instead of signing out, just redirect to signup
            navigate('/signup', { replace: true });
            return;
          }
          setError("Could not fetch user settings.");
          setIsLoading(false);
        }
      }
    };

    fetchUser();

    return () => {
      isMounted = false;
    };
  }, [navigate]);

  if (isLoading) {
    return <div className="settings-page">Loading settings...</div>;
  }

  return (
    <div className="settings-page">
      <h1>Settings</h1>

      {error && <p className="error">{error}</p>}

      <section className="settings-section">
        <h2>Profile</h2>
        <div className="settings-item">
          <span className="label">Email:</span>
          <span>{user?.email || "Not set"}</span>
        </div>
        <div className="settings-item">
          <span className="label">Display Name:</span>
          <span>{user?.displayName || "Not set"}</span>
        </div>
        <div className="settings-item">
          <span className="label">Timezone:</span>
          <span>{user?.timezone || "Not set"}</span>
        </div>
      </section>

      <section className="settings-section">
        <h2>Theme</h2>
        <div className="settings-item">
          <span className="label">Current Theme:</span>
          <span>{theme}</span>
          <button className="toggle-theme-button" onClick={setTheme}>
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
