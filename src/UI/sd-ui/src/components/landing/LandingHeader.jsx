import { useState } from "react";
import { Link } from "react-router-dom";
import "./LandingHeader.css";

function LandingHeader() {
  const [showLogin, setShowLogin] = useState(false);

  function toggleLoginPanel() {
    setShowLogin((prev) => !prev);
  }

  return (
    <header className="landing-header">
      <div className="landing-header-inner">
        <div className="logo">
          <Link to="/">sportDeets</Link>
        </div>

        <div className="header-actions">
          <button
            onClick={toggleLoginPanel}
            className={`signin-button ${showLogin ? "active" : ""}`}
          >
            Sign In
          </button>

          {/* Always render */}
          <div className={`login-dropdown ${showLogin ? "show" : ""}`}>
            <p>
              <strong>Sign In</strong>
            </p>
            <input type="email" placeholder="Email" />
            <input type="password" placeholder="Password" />
            <button className="submit-button">Login</button>
            <p className="login-footer-text">
              New here? <Link to="/signup">Get Started Free</Link>
            </p>
          </div>
        </div>
      </div>
    </header>
  );
}

export default LandingHeader;
