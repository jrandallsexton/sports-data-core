import { useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { FaGoogle, FaFacebook, FaGithub, FaApple } from "react-icons/fa";
import { getAuth, GoogleAuthProvider, signInWithPopup } from "firebase/auth";
import Login from "../login/Login.jsx";
import UserSummaryCard from "../usersummary/UserSummaryCard.jsx";
import "./SignupPage.css";
import apiWrapper from "../../api/apiWrapper";

function SignupPage() {
  const [firebaseUser, setFirebaseUser] = useState(null);
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
      const result = await signInWithPopup(auth, provider);
      const token = await result.user.getIdToken();

      // Send token to backend to set HttpOnly cookie
      await apiWrapper.Auth.setToken(token);

      // 🔍 Check if backend has this user
      const response = await fetch("/api/user/me", {
        credentials: "include", // Include cookies in the request
      });

      if (response.status === 404) {
        setFirebaseUser(result.user); // Show onboarding
      } else if (response.ok) {
        const redirectPath = location.state?.from?.pathname || "/app";
        navigate(redirectPath);
      } else {
        throw new Error(`Unexpected status ${response.status}`);
      }
    } catch (err) {
      console.error(err);
      alert("Sign-in failed.");
    }
  }

  function handleOnboardingSubmit(data) {
    console.log("User onboarding data:", data);
    // TODO: Save user data to backend via API
    // TODO: Navigate to /app or show success screen
  }

  // 🔀 Show onboarding if Firebase user is ready
  if (firebaseUser) {
    return (
      <div className="signup-page">
        <div className="signup-card">
          <UserSummaryCard
            user={firebaseUser}
            onSubmit={handleOnboardingSubmit}
          />
        </div>
      </div>
    );
  }

  return (
    <div className="signup-page">
      <div className="signup-card">
        <h2>Join sportDeets<span className="tm-symbol">™</span>!</h2>
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
          >
            <FaFacebook className="icon" /> Continue with Facebook (coming soon)
          </button>
          <button
            className="third-party-button apple disabled"
            onClick={() => handleThirdPartySignIn("Apple")}
          >
            <FaApple className="icon" /> Continue with Apple (coming soon)
          </button>
        </div>

        <hr className="divider" />

        <p>Prefer using your email?</p>
        <button className="email-signup-button">Sign up with Email</button>

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
