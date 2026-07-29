import { useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import {
  getAuth,
  createUserWithEmailAndPassword,
  updateProfile,
} from "firebase/auth";
import { FaEnvelope } from "react-icons/fa";
import "./EmailSignupForm.css"; // reuse existing styling

function EmailSignupForm({ onCancel }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const navigate = useNavigate();
  const location = useLocation();

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setLoading(true);

    console.groupCollapsed("[EmailSignupForm] Submission");
    console.log("Attempting signup for:", email);

    try {
      const auth = getAuth();
      const result = await createUserWithEmailAndPassword(
        auth,
        email,
        password
      );

      console.log("✔️ Firebase user created:", result.user.uid);

      // if (displayName) {
      //   console.log("🔧 Updating Firebase displayName to:", displayName);
      //   await updateProfile(result.user, { displayName });
      //   console.log("✔️ updateProfile successful.");
      // }

      if (displayName) {
        console.log("🔧 Updating Firebase displayName to:", displayName);
        await updateProfile(result.user, { displayName });
        console.log("✔️ updateProfile successful.");

        // ✅ Force user reload to ensure claims are updated server-side
        await result.user.reload();
        console.log("🔁 Firebase user reloaded.");
      }

      // The backend user is provisioned server-side on the first
      // authenticated request (FirebaseAuthenticationMiddleware →
      // UserService.GetOrCreateUserAsync) — the client just navigates.
      const redirectPath = location.state?.from?.pathname || "/app";
      console.log("🚀 Redirecting to:", redirectPath);
      navigate(redirectPath);
    } catch (err) {
      console.error("❌ Signup error:", err);
      setError(err.message || "Signup failed.");
    } finally {
      console.groupEnd();
      setLoading(false);
    }
  }

  return (
    <form className="email-signup-form" onSubmit={handleSubmit}>
      <h3>Sign up with Email</h3>

      {error && <div className="error-message">{error}</div>}

      <input
        type="text"
        placeholder="Display Name"
        value={displayName}
        onChange={(e) => setDisplayName(e.target.value)}
        required
      />

      <input
        type="email"
        placeholder="Email Address"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        required
      />

      <input
        type="password"
        placeholder="Create Password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        required
        minLength={6}
      />

      <button
        type="submit"
        className="third-party-button email"
        disabled={loading}
      >
        <FaEnvelope className="icon" />
        {loading ? "Signing up..." : "Create Account"}
      </button>

      <button
        type="button"
        className="third-party-button cancel-button"
        onClick={onCancel}
      >
        Cancel
      </button>
    </form>
  );
}

export default EmailSignupForm;
