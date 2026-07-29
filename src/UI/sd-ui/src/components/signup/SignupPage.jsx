import { useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { FaGoogle, FaFacebook, FaApple } from "react-icons/fa";
import { getAuth, GoogleAuthProvider, signInWithPopup } from "firebase/auth";
import Login from "../login/Login.jsx";
import "./SignupPage.css";
import EmailSignupForm from "./EmailSignupForm.jsx";

// Account provisioning is server-side: FirebaseAuthenticationMiddleware calls
// UserService.GetOrCreateUserAsync on the first authenticated request. The
// client does not create the backend user — it just signs in and navigates.
// (The cookie-exchange + /api/user/me probe + onboarding-card flow that used
// to live here was dead since the 2025-12-08 header-based auth migration.)
function SignupPage() {
  const [showEmailForm, setShowEmailForm] = useState(false);

  const navigate = useNavigate();
  const location = useLocation();

  async function handleThirdPartySignIn(providerName) {
    const auth = getAuth();
    let provider;

    switch (providerName) {
      case "Google":
        provider = new GoogleAuthProvider();
        break;
      case "Facebook":
        alert("Facebook not implemented yet.");
        return;
      default:
        alert(`${providerName} sign-in not implemented.`);
        return;
    }

    try {
      await signInWithPopup(auth, provider);
      const redirectPath = location.state?.from?.pathname || "/app";
      navigate(redirectPath);
    } catch (err) {
      console.error(err);
      alert("Sign-in failed.");
    }
  }

  return (
    <div className="signup-page">
      <div className="signup-card">
        <h2>
          Join sportDeets<span className="tm-symbol">™</span>!
        </h2>
        <p>Sign up free to start making your picks and saving insights.</p>

        <div className="third-party-buttons">
          <button
            className="third-party-button google"
            onClick={() => handleThirdPartySignIn("Google")}
          >
            <FaGoogle className="icon" /> Continue with Google
          </button>
          <button
            className="third-party-button facebook disabled"
            onClick={() => handleThirdPartySignIn("Facebook")}
            disabled
          >
            <FaFacebook className="icon" /> Continue with Facebook (coming soon)
          </button>
          <button
            className="third-party-button apple disabled"
            onClick={() => handleThirdPartySignIn("Apple")}
            disabled
          >
            <FaApple className="icon" /> Continue with Apple (coming soon)
          </button>
        </div>

        <hr className="divider" />

        {!showEmailForm ? (
          <>
            <p>Prefer using your email?</p>
            <button
              className="email-signup-button"
              onClick={() => setShowEmailForm(true)}
            >
              Sign up with Email
            </button>
          </>
        ) : (
          <EmailSignupForm onCancel={() => setShowEmailForm(false)} />
        )}

        <hr className="divider" />
        <p className="switch-to-login">Already have an account?</p>
        <div className="login-section">
          <Login />
        </div>
      </div>
    </div>
  );
}

export default SignupPage;
