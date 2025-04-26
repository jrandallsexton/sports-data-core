import { FaGoogle, FaFacebook, FaGithub, FaApple } from "react-icons/fa";
import "./SignupPage.css";

function SignupPage() {
  function handleThirdPartySignIn(provider) {
    alert(`Sign in with ${provider} clicked!`);
    // Later you replace this with real auth logic
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
        <button className="email-signup-button">
          Sign up with Email
        </button>
      </div>
    </div>
  );
}

export default SignupPage;
