import { useEffect, useState, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useTheme } from "../../contexts/ThemeContext";
import { useUserDto } from "../../contexts/UserContext";
import apiWrapper from "../../api/apiWrapper";
import { DEFAULT_TIMEZONE } from "../../utils/timeUtils";
import "./SettingsPage.css";
import BadgesPanel from "../../components/badges/BadgesPanel.tsx";

const CURATED_TIMEZONES = [
  { value: "America/New_York",    label: "Eastern (New York)" },
  { value: "America/Chicago",     label: "Central (Chicago)" },
  { value: "America/Denver",      label: "Mountain (Denver)" },
  { value: "America/Phoenix",     label: "Mountain - no DST (Phoenix)" },
  { value: "America/Los_Angeles", label: "Pacific (Los Angeles)" },
  { value: "America/Anchorage",   label: "Alaska (Anchorage)" },
  { value: "Pacific/Honolulu",    label: "Hawaii (Honolulu)" },
];

function getAllIanaZones() {
  if (typeof Intl?.supportedValuesOf === "function") {
    try {
      return Intl.supportedValuesOf("timeZone");
    } catch {
      return [];
    }
  }
  return [];
}

function detectBrowserTimezone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || DEFAULT_TIMEZONE;
  } catch {
    return DEFAULT_TIMEZONE;
  }
}

function SettingsPage() {
  const navigate = useNavigate();
  const { theme, toggleTheme } = useTheme();
  const { refreshUserDto } = useUserDto();
  const [user, setUser] = useState(null);
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(true);

  const [showAllZones, setShowAllZones] = useState(false);
  const [tzSaving, setTzSaving] = useState(false);
  const [tzMessage, setTzMessage] = useState("");

  const allZones = useMemo(() => getAllIanaZones(), []);
  const browserTz = useMemo(() => detectBrowserTimezone(), []);

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

  const effectiveTimezone = user?.timezone || browserTz;
  const isCurated = CURATED_TIMEZONES.some((z) => z.value === effectiveTimezone);

  const handleTimezoneChange = async (newTz) => {
    if (!newTz) return;
    setTzSaving(true);
    setTzMessage("");
    try {
      await apiWrapper.Users.updateTimezone(newTz);
      setUser((prev) => (prev ? { ...prev, timezone: newTz } : prev));
      await refreshUserDto();
      setTzMessage("Saved.");
    } catch (err) {
      console.error("Failed to update timezone:", err);
      setTzMessage("Could not save timezone.");
    } finally {
      setTzSaving(false);
    }
  };

  if (isLoading) {
    return <div className="settings-page">Loading settings...</div>;
  }

  return (
    <div className="settings-page">
      <h2>Settings</h2>

      {error && <p className="error">{error}</p>}

      <div className="settings-grid">
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
            <span>
              {(showAllZones || !isCurated) && allZones.length > 0 ? (
                <select
                  value={effectiveTimezone}
                  onChange={(e) => handleTimezoneChange(e.target.value)}
                  disabled={tzSaving}
                >
                  {allZones.map((z) => (
                    <option key={z} value={z}>{z}</option>
                  ))}
                </select>
              ) : (
                <select
                  value={effectiveTimezone}
                  onChange={(e) => {
                    if (e.target.value === "__other__") {
                      setShowAllZones(true);
                    } else {
                      handleTimezoneChange(e.target.value);
                    }
                  }}
                  disabled={tzSaving}
                >
                  {/* Fallback option for a non-curated saved zone (e.g.
                      Europe/London) when the host lacks Intl.supportedValuesOf —
                      without this the <select> has no matching <option> and
                      visually defaults to Eastern, hiding the user's real saved
                      value. */}
                  {!isCurated && (
                    <option value={effectiveTimezone}>{effectiveTimezone}</option>
                  )}
                  {CURATED_TIMEZONES.map((z) => (
                    <option key={z.value} value={z.value}>{z.label}</option>
                  ))}
                  <option value="__other__">Other…</option>
                </select>
              )}
              {!user?.timezone && (
                <span style={{ marginLeft: 8, fontSize: "0.85em", opacity: 0.7 }}>
                  (using browser default — pick one to save)
                </span>
              )}
              {tzMessage && (
                <span style={{ marginLeft: 8, fontSize: "0.85em" }}>{tzMessage}</span>
              )}
            </span>
          </div>
        </section>

        <section className="settings-section">
          <h2>Theme</h2>
          <div className="settings-item">
            <span className="label">Theme:</span>
            <div className="theme-toggle" onClick={toggleTheme} role="button" tabIndex={0} onKeyDown={(e) => e.key === 'Enter' && toggleTheme()}>
              <span className={`theme-toggle-option ${theme === 'light' ? 'active' : ''}`}>Light</span>
              <span className={`theme-toggle-option ${theme === 'dark' ? 'active' : ''}`}>Dark</span>
            </div>
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
      </div>

      <BadgesPanel />
    </div>
  );
}

export default SettingsPage;
