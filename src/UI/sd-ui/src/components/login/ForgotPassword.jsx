// src/components/login/ForgotPassword.jsx
import React, { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { sendPasswordResetEmail } from "firebase/auth";
import { auth } from "../../firebase";
import { FaEnvelope } from "react-icons/fa";
import "./Login.css";

const ForgotPassword = () => {
  const location = useLocation();
  const navigate = useNavigate();

  // The sign-in form hands over whatever address was already typed so the user
  // doesn't retype it after a failed login.
  const [email, setEmail] = useState(location.state?.email ?? "");
  const [sent, setSent] = useState(false);
  const [errorMsg, setErrorMsg] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setErrorMsg("");
    setSubmitting(true);

    try {
      await sendPasswordResetEmail(auth, email.trim());
      setSent(true);
    } catch (error) {
      const code = error?.code ?? "";

      // Never disclose whether an address has an account. An unknown address
      // gets the same confirmation a real one does — otherwise this page
      // becomes an account-enumeration oracle. (Firebase projects with email
      // enumeration protection enabled already return success here; this keeps
      // the behavior identical either way.)
      if (code === "auth/user-not-found" || code === "auth/invalid-email") {
        setSent(true);
      } else if (code === "auth/too-many-requests") {
        setErrorMsg("Too many attempts. Please wait a few minutes and try again.");
      } else if (code === "auth/network-request-failed") {
        setErrorMsg("Network error. Check your connection and try again.");
      } else {
        // Deliberately not error.message — Firebase's raw strings leak
        // internals like "Firebase: Error (auth/...)".
        setErrorMsg("Something went wrong. Please try again.");
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (sent) {
    return (
      <div className="login-page">
        <div className="login-card">
          <h2>Check your email</h2>
          <p className="forgot-blurb">
            If an account exists for <strong>{email.trim()}</strong>, we&apos;ve sent a
            link to reset your password. It may take a minute to arrive — check your
            spam folder if you don&apos;t see it.
          </p>
          <button
            type="button"
            className="login-button"
            onClick={() => navigate("/signup")}
          >
            Back to Sign In
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <h2>Reset your password</h2>
        <p className="forgot-blurb">
          Enter your email and we&apos;ll send you a link to reset your password.
        </p>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="reset-email">Email:</label>
            <div className="input-wrapper">
              <FaEnvelope />
              <input
                id="reset-email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                autoComplete="email"
                required
              />
            </div>
          </div>

          {errorMsg && <div className="error-message">{errorMsg}</div>}

          <button type="submit" className="login-button" disabled={submitting}>
            {submitting ? "Sending…" : "Send reset link"}
          </button>
        </form>

        <p className="forgot-back">
          <Link to="/signup">← Back to sign in</Link>
        </p>
      </div>
    </div>
  );
};

export default ForgotPassword;
