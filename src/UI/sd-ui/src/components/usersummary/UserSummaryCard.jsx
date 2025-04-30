import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper";
import "./UserSummaryCard.css";

function UserSummaryCard({ firebaseUser }) {
  const navigate = useNavigate();

  const [formData, setFormData] = useState({
    displayName: "",
    email: "",
    timezone: Intl.DateTimeFormat().resolvedOptions().timeZone || "",
  });

  const [error, setError] = useState("");

  useEffect(() => {
    if (firebaseUser) {
      setFormData({
        displayName: firebaseUser.displayName || "",
        email: firebaseUser.email || "",
        timezone: Intl.DateTimeFormat().resolvedOptions().timeZone || "",
      });
    }
  }, [firebaseUser]);

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

  if (!firebaseUser) {
    return null; // or a loader if preferred
  }

  return (
    <div className="user-summary-card">
      {firebaseUser.photoURL ? (
        <img
          src={firebaseUser.photoURL}
          alt="Profile"
          className="profile-image"
        />
      ) : (
        <div className="default-avatar">👤</div>
      )}

      <h3>Welcome!</h3>
      <p>Just a few more details to complete your profile.</p>

      <form onSubmit={handleSubmit}>
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
          <input
            type="text"
            name="timezone"
            value={formData.timezone}
            onChange={handleChange}
            required
          />
        </label>

        <label>
          Email (read-only):
          <input type="email" value={formData.email} readOnly />
        </label>

        {error && <p className="error">{error}</p>}

        <button type="submit">Continue to App</button>
      </form>
    </div>
  );
}

export default UserSummaryCard;
