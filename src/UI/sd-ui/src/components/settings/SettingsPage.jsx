import { useEffect, useState, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { getAuth, signOut } from "firebase/auth";
import { toast } from "react-hot-toast";
import { useTheme } from "../../contexts/ThemeContext";
import { useUserDto } from "../../contexts/UserContext";
import apiWrapper from "../../api/apiWrapper";
import { DEFAULT_TIMEZONE } from "../../utils/timeUtils";
import "./SettingsPage.css";
import BadgesPanel from "../../components/badges/BadgesPanel.tsx";

// Notification categories, in the same order as the mobile settings screen.
// The API owns these flags (canonical) and projects changes to the Notification
// service, which gates sends. Six are actively enforced today; matchup previews
// and schedule changes are projected-but-not-yet-gated (exposed for parity).
// See docs/mobile/notification-preferences.md.
const NOTIFICATION_CATEGORIES = [
  { key: "pickResultEnabled", label: "Pick results" },
  { key: "pickDeadlineReminderEnabled", label: "Pick deadline reminders" },
  { key: "contestStartReminderEnabled", label: "Kickoff reminders" },
  { key: "leagueInviteEnabled", label: "League invites" },
  { key: "membershipEnabled", label: "League membership updates" },
  { key: "matchupPreviewEnabled", label: "Matchup previews" },
  { key: "scheduleChangeEnabled", label: "Schedule changes" },
  { key: "oddsChangedEnabled", label: "Line moves" },
];

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

  const [displayNameInput, setDisplayNameInput] = useState("");
  const [displayNameSaving, setDisplayNameSaving] = useState(false);
  const [displayNameMessage, setDisplayNameMessage] = useState("");

  const [prefs, setPrefs] = useState(null);
  const [prefsError, setPrefsError] = useState("");
  const [prefsSaving, setPrefsSaving] = useState(false);
  const [prefsMessage, setPrefsMessage] = useState("");

  const [deleteConfirming, setDeleteConfirming] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState("");

  const allZones = useMemo(() => getAllIanaZones(), []);
  const browserTz = useMemo(() => detectBrowserTimezone(), []);

  useEffect(() => {
    let isMounted = true;

    const fetchUser = async () => {
      try {
        const response = await apiWrapper.Users.getCurrentUser();
        if (isMounted) {
          setUser(response.data);
          setDisplayNameInput(response.data?.displayName || "");
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

  useEffect(() => {
    let isMounted = true;

    const fetchPrefs = async () => {
      try {
        const response = await apiWrapper.Users.getNotificationPreferences();
        if (isMounted) setPrefs(response.data);
      } catch (err) {
        console.error("Failed to load notification preferences:", err);
        // Leave prefs null so the section shows an error rather than a
        // misleading all-on state the user might accidentally save over.
        if (isMounted) setPrefsError("Could not load notification settings.");
      }
    };

    fetchPrefs();

    return () => {
      isMounted = false;
    };
  }, []);

  const effectiveTimezone = user?.timezone || browserTz;
  const isCurated = CURATED_TIMEZONES.some((z) => z.value === effectiveTimezone);

  const handleDisplayNameSave = async () => {
    const next = displayNameInput.trim();
    if (!next || next === (user?.displayName || "")) return;
    setDisplayNameSaving(true);
    setDisplayNameMessage("");
    try {
      await apiWrapper.Users.updateDisplayName(next);
      setUser((prev) => (prev ? { ...prev, displayName: next } : prev));
      setDisplayNameInput(next);
      await refreshUserDto();
      setDisplayNameMessage("Saved.");
    } catch (err) {
      console.error("Failed to update display name:", err);
      // ToActionResult() returns validation failures as an array:
      // { errors: [ { propertyName, errorMessage }, ... ] }.
      const serverMsg =
        err?.response?.data?.errors?.find?.(
          (e) => e.propertyName === "DisplayName"
        )?.errorMessage ||
        err?.response?.data?.title;
      setDisplayNameMessage(serverMsg || "Could not save display name.");
    } finally {
      setDisplayNameSaving(false);
    }
  };

  const handleToggleNotification = async (key) => {
    if (!prefs || prefsSaving) return;
    const previous = prefs;
    // Full-replacement PATCH — send the whole set with this one flag flipped.
    const next = { ...prefs, [key]: !prefs[key] };
    setPrefs(next); // optimistic
    setPrefsSaving(true);
    setPrefsMessage("");
    try {
      await apiWrapper.Users.updateNotificationPreferences(next);
      setPrefsMessage("Saved.");
    } catch (err) {
      console.error("Failed to update notification preferences:", err);
      setPrefs(previous); // revert
      setPrefsMessage("Could not save. Please try again.");
    } finally {
      setPrefsSaving(false);
    }
  };

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

  const handleDeleteAccount = async () => {
    setDeleting(true);
    setDeleteError("");
    try {
      await apiWrapper.Users.deleteAccount();
    } catch (err) {
      console.error("Account deletion failed:", err);
      setDeleteError("We could not delete your account. Please try again.");
      setDeleting(false);
      return;
    }
    // The account (including the Firebase login) is gone server-side. Tear the
    // local session down the same way sign-out does. clear-token is best-effort;
    // it must not block the load-bearing Firebase sign-out, so each runs in its
    // own try/catch.
    try {
      await apiWrapper.Auth.clearToken();
    } catch (err) {
      console.warn("clear-token after deletion failed (continuing to sign-out):", err);
    }
    try {
      await signOut(getAuth());
    } catch (err) {
      console.warn("Firebase sign-out after deletion failed (account already gone):", err);
    }
    toast.success("Your account has been deleted.");
    navigate("/");
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
            <span className="label">Username:</span>
            <span>{user?.username ? `@${user.username}` : "Not set"}</span>
          </div>
          <div className="settings-item">
            <span className="label">Display Name:</span>
            <span>
              <input
                type="text"
                aria-label="Display Name"
                value={displayNameInput}
                onChange={(e) => setDisplayNameInput(e.target.value)}
                disabled={displayNameSaving}
                maxLength={25}
                style={{ marginRight: 8 }}
              />
              <button onClick={handleDisplayNameSave} disabled={displayNameSaving}>
                {displayNameSaving ? "Saving…" : "Save"}
              </button>
              {displayNameMessage && (
                <span style={{ marginLeft: 8, fontSize: "0.85em" }}>{displayNameMessage}</span>
              )}
            </span>
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
          <p style={{ fontSize: "0.85em", opacity: 0.7, marginTop: 0 }}>
            Choose which push notifications you receive on your devices.
          </p>
          {prefsError ? (
            <p className="error">{prefsError}</p>
          ) : !prefs ? (
            <p style={{ fontSize: "0.85em", opacity: 0.7 }}>Loading…</p>
          ) : (
            <>
              {NOTIFICATION_CATEGORIES.map(({ key, label }) => (
                <div className="settings-item" key={key}>
                  <span className="label">{label}:</span>
                  <input
                    type="checkbox"
                    aria-label={label}
                    checked={!!prefs[key]}
                    disabled={prefsSaving}
                    onChange={() => handleToggleNotification(key)}
                  />
                </div>
              ))}
              {prefsMessage && (
                <span style={{ fontSize: "0.85em" }}>{prefsMessage}</span>
              )}
            </>
          )}
        </section>

        <section className="settings-section">
          <h2>Account</h2>
          {!deleteConfirming ? (
            <div className="settings-item">
              <span className="label">Delete Account:</span>
              <button
                onClick={() => {
                  setDeleteError("");
                  setDeleteConfirming(true);
                }}
              >
                Delete…
              </button>
            </div>
          ) : (
            <div>
              <p style={{ fontSize: "0.85em", marginTop: 0 }}>
                This permanently deletes your account and removes your personal
                data. Your league history stays (anonymized). This cannot be undone.
              </p>
              <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                <button
                  className="settings-button-secondary"
                  onClick={() => setDeleteConfirming(false)}
                  disabled={deleting}
                >
                  Cancel
                </button>
                <button
                  className="settings-button-danger"
                  onClick={handleDeleteAccount}
                  disabled={deleting}
                >
                  {deleting ? "Deleting…" : "Yes, delete my account"}
                </button>
              </div>
              {deleteError && (
                <p className="error" style={{ marginTop: 8 }}>{deleteError}</p>
              )}
            </div>
          )}
        </section>
      </div>

      <BadgesPanel />
    </div>
  );
}

export default SettingsPage;
