import { useState } from "react";
import { useNavigate } from 'react-router-dom';
import { FaGoogle, FaFacebook, FaGithub, FaApple } from "react-icons/fa";
import { getAuth, GoogleAuthProvider, signInWithPopup } from "firebase/auth";
import Login from "../login/Login.jsx";
import UserSummaryCard from "../usersummary/UserSummaryCard.jsx";
import "./SignupPage.css";

function SignupPage() {
  
  const [firebaseUser, setFirebaseUser] = useState(null);
  const navigate = useNavigate();

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
      localStorage.setItem("authToken", token);

      // 🔍 Check if backend has this user
      const response = await fetch("/api/user/me", {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      });

      if (response.status === 404) {
        setFirebaseUser(result.user); // Show onboarding
      } else if (response.ok) {
        navigate("/app"); // Skip onboarding
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
        <h2>Join sportDeets!</h2>
        <p>Sign up free to start making your picks and saving insights.</p>

        <div className="third-party-buttons">
          <button
            className="third-party-button google"
            onClick={() => handleThirdPartySignIn("Google")}
          >
            <FaGoogle className="icon" /> Continue with Google
          </button>
          <button
            className="third-party-button facebook"
            onClick={() => handleThirdPartySignIn("Facebook")}
          >
            <FaFacebook className="icon" /> Continue with Facebook
          </button>
          <button
            className="third-party-button github"
            onClick={() => handleThirdPartySignIn("GitHub")}
          >
            <FaGithub className="icon" /> Continue with GitHub
          </button>
          <button
            className="third-party-button apple"
            onClick={() => handleThirdPartySignIn("Apple")}
          >
            <FaApple className="icon" /> Continue with Apple
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
