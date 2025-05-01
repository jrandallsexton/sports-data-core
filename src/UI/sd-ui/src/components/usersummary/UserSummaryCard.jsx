import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper";
import moment from "moment-timezone";

import "./UserSummaryCard.css";

function UserSummaryCard({ user }) {
  const timezoneOptions = moment.tz.names();

  const navigate = useNavigate();

  const [formData, setFormData] = useState({
    displayName: "",
    email: "",
    timezone: Intl.DateTimeFormat().resolvedOptions().timeZone || "",
  });

  const [error, setError] = useState("");

  useEffect(() => {
    if (user) {
      setFormData({
        displayName: user.displayName || "",
        email: user.email || "",
        timezone: Intl.DateTimeFormat().resolvedOptions().timeZone || "",
      });
    }
  }, [user]);

  const handleChange = (e) => {
    setFormData((prev) => ({ ...prev, [e.target.name]: e.target.value }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    try {
      await apiWrapper.Users.createOrUpdateUser(formData);
      navigate("/app");
    } catch (err) {
      console.error(err);
      setError("Something went wrong. Please try again.");
    }
  };

  if (!user) return null;

  return (
    <div className="user-summary-card">
      {user.photoURL ? (
        <img src={user.photoURL} alt="Profile" className="profile-image" />
      ) : (
        <div className="default-avatar">👤</div>
      )}

      <h3>Welcome!</h3>
      <p>Just a few more details to complete your profile.</p>

      <form className="onboarding-form" onSubmit={handleSubmit}>
        <label>
          Display Name:
          <input
            type="text"
            name="displayName"
            value={formData.displayName}
            onChange={handleChange}
            required
          />
        </label>

        <label>
          Timezone:
          <select
            name="timezone"
            value={formData.timezone}
            onChange={handleChange}
            required
          >
            {timezoneOptions.map((tz) => (
              <option key={tz} value={tz}>
                {tz}
              </option>
            ))}
          </select>
        </label>

        <label>
          Email (read-only):
          <input type="email" value={formData.email} readOnly />
        </label>

        {error && <p className="error">{error}</p>}

        <button type="submit" className="submit-button">
          Continue to App
        </button>
      </form>
    </div>
  );
}

export default UserSummaryCard;
