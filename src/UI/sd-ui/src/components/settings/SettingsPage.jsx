import { useTheme } from "../../contexts/ThemeContext"; // ✅ correct import
import "./SettingsPage.css";

function SettingsPage() {
  const { theme, toggleTheme } = useTheme(); // ✅ useTheme directly

  return (
    <div className="settings-page">
      <h1>Settings</h1>

      {/* Profile Section */}
      <section className="settings-section">
        <h2>Profile</h2>
        <div className="settings-item">
          <label>Email:</label>
          <p>user@example.com</p> {/* Placeholder */}
        </div>
        <div className="settings-item">
          <label>Display Name:</label>
          <p>sportsFan123</p> {/* Placeholder */}
        </div>
      </section>

      {/* Theme Section */}
      <section className="settings-section">
        <h2>Theme</h2>
        <div className="settings-item">
          <label>Current Theme:</label>
          <p>{theme}</p>
          <button className="toggle-theme-button" onClick={toggleTheme}>
            Toggle Theme
          </button>
        </div>
      </section>

      {/* Notifications Section */}
      <section className="settings-section">
        <h2>Notifications</h2>
        <div className="settings-item">
          <label>Email Alerts:</label>
          <input type="checkbox" disabled />
        </div>
        <div className="settings-item">
          <label>Push Notifications:</label>
          <input type="checkbox" disabled />
        </div>
      </section>
    </div>
  );
}

export default SettingsPage;
